using EAFCMatchTracker.Application.Dtos;
using EAFCMatchTracker.Application.Interfaces.Repositories;
using EAFCMatchTracker.Application.Interfaces.Services;
using EAFCMatchTracker.Domain.Entities;
using EAFCMatchTracker.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EAFCMatchTracker.Application.Services;

public class GoalAnalysisService : IGoalAnalysisService
{
    private readonly IGoalRepository _goalRepository;
    private readonly IMatchRepository _matchRepository;
    private readonly EAFCContext _db;
    private readonly ILogger<GoalAnalysisService> _logger;

    public GoalAnalysisService(
        IGoalRepository goalRepository,
        IMatchRepository matchRepository,
        EAFCContext db,
        ILogger<GoalAnalysisService> logger)
    {
        _goalRepository = goalRepository;
        _matchRepository = matchRepository;
        _db = db;
        _logger = logger;
    }

    public async Task<GoalAnalysisResponseDto> GetGoalAnalysisAsync(long clubId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
    {
        _logger.LogInformation("GoalAnalysisService.GetGoalAnalysisAsync clubId={ClubId}", clubId);

        var matchesInRange = await _db.MatchClubs
            .AsNoTracking()
            .Where(mc => mc.ClubId == clubId
                      && mc.Match.Timestamp >= fromUtc
                      && mc.Match.Timestamp <= toUtc)
            .Select(mc => new { mc.MatchId, mc.Match.Timestamp, mc.Goals })
            .ToListAsync(ct);

        var matchIdList = matchesInRange.Select(m => m.MatchId).Distinct().ToList();
        var totalGoals = matchesInRange.Sum(m => (int)m.Goals);
        var timestampByMatch = matchesInRange
            .GroupBy(m => m.MatchId)
            .ToDictionary(g => g.Key, g => g.First().Timestamp);

        var goalLinks = await _goalRepository.GetGoalLinksByMatchIdsAsync(matchIdList, clubId, ct);

        var allMatchPlayers = await _db.MatchPlayers
            .AsNoTracking()
            .Include(mp => mp.Player)
            .Where(mp => matchIdList.Contains(mp.MatchId) && mp.ClubId == clubId)
            .ToListAsync(ct);

        var nameMap = allMatchPlayers
            .Where(mp => mp.Player != null)
            .GroupBy(mp => mp.PlayerEntityId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(mp => mp.MatchId)
                      .Select(mp => !string.IsNullOrWhiteSpace(mp.ProName) ? mp.ProName : mp.Player?.Playername)
                      .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)) ?? "");

        var involvedIds = goalLinks
            .SelectMany(g => new[] { (long?)g.ScorerPlayerEntityId, g.AssistPlayerEntityId, g.PreAssistPlayerEntityId })
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();

        var missingIds = involvedIds.Where(id => !nameMap.ContainsKey(id) || string.IsNullOrWhiteSpace(nameMap[id])).ToList();
        if (missingIds.Count > 0)
        {
            var fallbacks = await _db.Players
                .AsNoTracking()
                .Where(p => missingIds.Contains(p.Id))
                .Select(p => new { p.Id, p.Playername })
                .ToListAsync(ct);
            foreach (var f in fallbacks)
                if (!string.IsNullOrWhiteSpace(f.Playername))
                    nameMap[f.Id] = f.Playername;
        }

        string Resolve(long? id) =>
            id.HasValue && nameMap.TryGetValue(id.Value, out var n) && !string.IsNullOrWhiteSpace(n) ? n : null!;

        var linkDtos = goalLinks
            .Select(g => new GoalAnalysisLinkDto
            {
                MatchId = g.MatchId,
                MatchTimestamp = timestampByMatch.TryGetValue(g.MatchId, out var ts) ? ts : DateTime.MinValue,
                ScorerName = Resolve(g.ScorerPlayerEntityId) ?? "Desconhecido",
                AssistName = g.AssistPlayerEntityId.HasValue ? Resolve(g.AssistPlayerEntityId) : null,
                PreAssistName = g.PreAssistPlayerEntityId.HasValue ? Resolve(g.PreAssistPlayerEntityId) : null,
            })
            .OrderByDescending(l => l.MatchTimestamp)
            .ToList();

        var playerMap = allMatchPlayers
            .Where(mp => mp.Player != null)
            .GroupBy(mp => mp.Player.PlayerId)
            .Select(g =>
            {
                var repr = g.OrderByDescending(mp => mp.MatchId).First();
                var name = !string.IsNullOrWhiteSpace(repr.ProName)
                    ? repr.ProName
                    : repr.Player?.Playername ?? "Desconhecido";
                var goals    = g.Sum(mp => (int)mp.Goals);
                var assists  = g.Sum(mp => (int)mp.Assists);
                var pre      = g.Sum(mp => (int)mp.PreAssists);
                return new GoalAnalysisPlayerDto
                {
                    Name       = name,
                    Goals      = goals,
                    Assists    = assists,
                    PreAssists = pre,
                    Total      = goals + assists + pre,
                };
            })
            .Where(p => p.Total > 0)
            .ToDictionary(p => p.Name);

        var pairs = linkDtos
            .Where(l => !string.IsNullOrEmpty(l.AssistName))
            .GroupBy(l => (l.AssistName!, l.ScorerName))
            .Select(g => new GoalAnalysisPairDto { From = g.Key.Item1, To = g.Key.ScorerName, Count = g.Count() })
            .OrderByDescending(p => p.Count)
            .ToList();

        var trios = linkDtos
            .Where(l => !string.IsNullOrEmpty(l.PreAssistName) && !string.IsNullOrEmpty(l.AssistName))
            .GroupBy(l => (l.PreAssistName!, l.AssistName!, l.ScorerName))
            .Select(g => new GoalAnalysisTrioDto { Pre = g.Key.Item1, Assist = g.Key.Item2, Scorer = g.Key.ScorerName, Count = g.Count() })
            .OrderByDescending(t => t.Count)
            .ToList();

        return new GoalAnalysisResponseDto
        {
            ClubId = clubId,
            From = fromUtc,
            To = toUtc,
            TotalMatches = matchIdList.Count,
            TotalGoals = totalGoals,
            LinkedGoals = goalLinks.Count,
            TotalAssists = goalLinks.Count(g => g.AssistPlayerEntityId.HasValue),
            TotalPreAssists = goalLinks.Count(g => g.PreAssistPlayerEntityId.HasValue),
            Players = playerMap.Values.OrderByDescending(p => p.Total).ThenByDescending(p => p.Goals).ToList(),
            Pairs = pairs,
            Trios = trios,
            GoalLinks = linkDtos,
        };
    }

    public async Task<MatchGoalsResponseDto?> GetGoalsByMatchIdAsync(long matchId, CancellationToken ct)
    {
        _logger.LogInformation("GoalAnalysisService.GetGoalsByMatchIdAsync matchId={MatchId}", matchId);

        var match = await _db.Matches
            .Include(m => m.MatchPlayers)
            .FirstOrDefaultAsync(m => m.MatchId == matchId, ct);

        if (match == null) return null;

        var matchPlayers = match.MatchPlayers;
        var goals = await _goalRepository.GetGoalLinksByMatchIdAsync(matchId, ct);

        return new MatchGoalsResponseDto
        {
            MatchId = matchId,
            TotalGoals = goals.Count,
            Goals = goals.Select(g =>
            {
                var scorer = matchPlayers.FirstOrDefault(mp => mp.PlayerEntityId == g.ScorerPlayerEntityId);
                var assist = g.AssistPlayerEntityId.HasValue
                    ? matchPlayers.FirstOrDefault(mp => mp.PlayerEntityId == g.AssistPlayerEntityId)
                    : null;
                var preAssist = g.PreAssistPlayerEntityId.HasValue
                    ? matchPlayers.FirstOrDefault(mp => mp.PlayerEntityId == g.PreAssistPlayerEntityId)
                    : null;

                return new MatchGoalItemDto
                {
                    MatchId = g.MatchId,
                    ClubId = g.ClubId,
                    ScorerPlayerEntityId = g.ScorerPlayerEntityId,
                    ScorerName = scorer?.ProName,
                    AssistPlayerEntityId = g.AssistPlayerEntityId,
                    AssistName = assist?.ProName,
                    PreAssistPlayerEntityId = g.PreAssistPlayerEntityId,
                    PreAssistName = preAssist?.ProName
                };
            }).ToList()
        };
    }

    public async Task RegisterGoalsAsync(long matchId, RegisterGoalsRequest request, CancellationToken ct)
    {
        _logger.LogInformation("GoalAnalysisService.RegisterGoalsAsync matchId={MatchId}", matchId);

        var match = await _db.Matches
            .Include(m => m.MatchPlayers)
            .FirstOrDefaultAsync(m => m.MatchId == matchId, ct);

        if (match == null)
            throw new KeyNotFoundException($"Match {matchId} not found.");

        var mp = match.MatchPlayers;

        var realGoals = mp.ToDictionary(x => x.PlayerEntityId, x => x.Goals);
        var realAssists = mp.ToDictionary(x => x.PlayerEntityId, x => x.Assists);

        var reqGoals = new Dictionary<long, int>();
        var reqAssists = new Dictionary<long, int>();

        foreach (var g in request.Goals)
        {
            if (!reqGoals.ContainsKey(g.ScorerPlayerEntityId))
                reqGoals[g.ScorerPlayerEntityId] = 0;
            reqGoals[g.ScorerPlayerEntityId]++;

            if (g.AssistPlayerEntityId.HasValue)
            {
                long id = g.AssistPlayerEntityId.Value;
                if (!reqAssists.ContainsKey(id)) reqAssists[id] = 0;
                reqAssists[id]++;
            }
        }

        foreach (var kv in reqGoals)
        {
            if (!realGoals.ContainsKey(kv.Key))
                throw new ArgumentException($"Player {kv.Key} not found in match.");
            if (kv.Value > realGoals[kv.Key])
                throw new ArgumentException($"Player {kv.Key} cannot receive {kv.Value} goals (max {realGoals[kv.Key]}).");
        }

        foreach (var kv in reqAssists)
        {
            if (!realAssists.ContainsKey(kv.Key))
                throw new ArgumentException($"Player {kv.Key} not found in match.");
            if (kv.Value > realAssists[kv.Key])
                throw new ArgumentException($"Player {kv.Key} cannot receive {kv.Value} assists (max {realAssists[kv.Key]}).");
        }

        long clubId = mp.First().ClubId;

        foreach (var g in request.Goals)
        {
            var entry = new MatchGoalLinkEntity
            {
                MatchId = matchId,
                ClubId = clubId,
                ScorerPlayerEntityId = g.ScorerPlayerEntityId,
                AssistPlayerEntityId = g.AssistPlayerEntityId,
                PreAssistPlayerEntityId = g.PreAssistPlayerEntityId
            };

            await _goalRepository.AddGoalLinkAsync(entry, ct);

            if (g.PreAssistPlayerEntityId.HasValue)
            {
                var mpItem = mp.FirstOrDefault(x => x.PlayerEntityId == g.PreAssistPlayerEntityId.Value);
                if (mpItem != null)
                    mpItem.PreAssists++;
            }
        }

        await _goalRepository.SaveChangesAsync(ct);
    }
}
