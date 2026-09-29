using EAFCMatchTracker.Application.Dtos;
using EAFCMatchTracker.Application.Exceptions;
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
        var playerIdMap = allMatchPlayers.Where(mp => mp.Player != null)
            .GroupBy(mp => mp.PlayerEntityId)
            .ToDictionary(g => g.Key, g => g.First().Player.PlayerId);

        var involvedIds = goalLinks
            .SelectMany(g => new[] { (long?)g.ScorerPlayerEntityId, g.AssistPlayerEntityId, g.PreAssistPlayerEntityId })
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();

        var missingIds = involvedIds.Where(id => !nameMap.ContainsKey(id) || string.IsNullOrWhiteSpace(nameMap[id])).ToList();
        if (missingIds.Count > 0)
        {
            var fallbacks = await _db.Players
                .AsNoTracking()
                .Where(p => missingIds.Contains(p.Id))
                .Select(p => new { p.Id, p.PlayerId, p.Playername })
                .ToListAsync(ct);
            foreach (var f in fallbacks)
            {
                playerIdMap[f.Id] = f.PlayerId;
                if (!string.IsNullOrWhiteSpace(f.Playername))
                    nameMap[f.Id] = f.Playername;
            }
        }

        string Resolve(long? id) =>
            id.HasValue && nameMap.TryGetValue(id.Value, out var n) && !string.IsNullOrWhiteSpace(n) ? n : null!;
        long ResolveId(long id) => playerIdMap.TryGetValue(id, out var playerId) && playerId > 0 ? playerId : -id;

        var linkDtos = goalLinks
            .Select(g => new GoalAnalysisLinkDto
            {
                MatchId = g.MatchId,
                MatchTimestamp = timestampByMatch.TryGetValue(g.MatchId, out var ts) ? ts : DateTime.MinValue,
                ScorerId = ResolveId(g.ScorerPlayerEntityId),
                AssistId = g.AssistPlayerEntityId.HasValue ? ResolveId(g.AssistPlayerEntityId.Value) : null,
                PreAssistId = g.PreAssistPlayerEntityId.HasValue ? ResolveId(g.PreAssistPlayerEntityId.Value) : null,
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
                    PlayerId   = g.Key,
                    Name       = name,
                    Goals      = goals,
                    Assists    = assists,
                    PreAssists = pre,
                    Total      = goals + assists + pre,
                };
            })
            .Where(p => p.Total > 0)
            .ToList(); // lista: nomes de exibição podem repetir/estar vazios (antes ToDictionary(p => p.Name) estourava)

        var pairs = linkDtos
            .Where(l => l.AssistId.HasValue)
            .GroupBy(l => (l.AssistId!.Value, l.ScorerId))
            .Select(g => new GoalAnalysisPairDto { FromId = g.Key.Item1, ToId = g.Key.ScorerId, From = g.First().AssistName ?? "Desconhecido", To = g.First().ScorerName, Count = g.Count() })
            .OrderByDescending(p => p.Count)
            .ToList();

        var trios = linkDtos
            .Where(l => l.PreAssistId.HasValue && l.AssistId.HasValue)
            .GroupBy(l => (l.PreAssistId!.Value, l.AssistId!.Value, l.ScorerId))
            .Select(g => new GoalAnalysisTrioDto { PreId = g.Key.Item1, AssistId = g.Key.Item2, ScorerId = g.Key.ScorerId, Pre = g.First().PreAssistName ?? "Desconhecido", Assist = g.First().AssistName ?? "Desconhecido", Scorer = g.First().ScorerName, Count = g.Count() })
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
            Players = playerMap.OrderByDescending(p => p.Total).ThenByDescending(p => p.Goals).ToList(),
            Pairs = pairs,
            Trios = trios,
            GoalLinks = linkDtos,
        };
    }

    public async Task<MatchGoalsResponseDto?> GetGoalsByMatchIdAsync(long matchId, CancellationToken ct)
    {
        _logger.LogInformation("GoalAnalysisService.GetGoalsByMatchIdAsync matchId={MatchId}", matchId);

        var match = await _db.Matches
            .AsNoTracking()
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

    /// <summary>
    /// Registra os gols de uma partida. Idempotente por clube: os links já existentes dos clubes presentes no
    /// payload são SUBSTITUÍDOS pelos enviados (o frontend reenvia o conjunto completo), e PreAssists dos
    /// jogadores desses clubes é recalculado a partir dos links finais (nada é incrementado em duplicidade).
    /// O ClubId de cada link é o do artilheiro. Tudo em um único SaveChanges (transação).
    /// </summary>
    public async Task RegisterGoalsAsync(long matchId, RegisterGoalsRequest request, CancellationToken ct)
    {
        _logger.LogInformation("GoalAnalysisService.RegisterGoalsAsync matchId={MatchId}", matchId);

        var match = await _db.Matches
            .Include(m => m.MatchPlayers)
            .FirstOrDefaultAsync(m => m.MatchId == matchId, ct);

        if (match == null)
            throw new KeyNotFoundException($"Match {matchId} not found.");

        var goals = request.Goals ?? new List<GoalRegistrationDto>();
        var players = match.MatchPlayers.ToList();
        var byPlayer = players
            .GroupBy(x => x.PlayerEntityId)
            .ToDictionary(g => g.Key, g => g.First());

        // 1) Validação (ids pertencem à partida, mesmo clube do artilheiro, limites de gols/assistências)
        foreach (var g in goals)
        {
            if (!byPlayer.TryGetValue(g.ScorerPlayerEntityId, out var scorer))
                throw new DomainValidationException($"Player {g.ScorerPlayerEntityId} not found in match.");

            if (g.AssistPlayerEntityId.HasValue)
            {
                var id = g.AssistPlayerEntityId.Value;
                if (!byPlayer.TryGetValue(id, out var assist))
                    throw new DomainValidationException($"Assist player {id} not found in match.");
                if (assist.ClubId != scorer.ClubId)
                    throw new DomainValidationException($"Assist player {id} does not belong to the scorer's club.");
                if (id == g.ScorerPlayerEntityId)
                    throw new DomainValidationException($"Player {id} cannot assist their own goal.");
            }

            if (g.PreAssistPlayerEntityId.HasValue)
            {
                var id = g.PreAssistPlayerEntityId.Value;
                if (!byPlayer.TryGetValue(id, out var pre))
                    throw new DomainValidationException($"Pre-assist player {id} not found in match.");
                if (pre.ClubId != scorer.ClubId)
                    throw new DomainValidationException($"Pre-assist player {id} does not belong to the scorer's club.");
                if (id == g.ScorerPlayerEntityId || id == g.AssistPlayerEntityId)
                    throw new DomainValidationException($"Pre-assist player {id} must differ from the scorer and the assist player.");
            }
        }

        var reqGoals = goals.GroupBy(g => g.ScorerPlayerEntityId).ToDictionary(g => g.Key, g => g.Count());
        var reqAssists = goals
            .Where(g => g.AssistPlayerEntityId.HasValue)
            .GroupBy(g => g.AssistPlayerEntityId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var kv in reqGoals)
        {
            var real = byPlayer[kv.Key].Goals;
            if (kv.Value > real)
                throw new DomainValidationException($"Player {kv.Key} cannot receive {kv.Value} goals (max {real}).");
        }

        foreach (var kv in reqAssists)
        {
            var real = byPlayer[kv.Key].Assists;
            if (kv.Value > real)
                throw new DomainValidationException($"Player {kv.Key} cannot receive {kv.Value} assists (max {real}).");
        }

        // 2) Substitui os links dos clubes presentes no payload (inclui links legados gravados com ClubId errado)
        var affectedClubs = goals.Select(g => byPlayer[g.ScorerPlayerEntityId].ClubId).Distinct().ToList();
        var affectedPlayerIds = players.Where(x => affectedClubs.Contains(x.ClubId)).Select(x => x.PlayerEntityId).ToList();

        var existingLinks = await _db.MatchGoalLinks
            .Where(l => l.MatchId == matchId
                     && (affectedClubs.Contains(l.ClubId) || affectedPlayerIds.Contains(l.ScorerPlayerEntityId)))
            .ToListAsync(ct);
        _db.MatchGoalLinks.RemoveRange(existingLinks);

        var newLinks = goals.Select(g => new MatchGoalLinkEntity
        {
            MatchId = matchId,
            ClubId = byPlayer[g.ScorerPlayerEntityId].ClubId,
            ScorerPlayerEntityId = g.ScorerPlayerEntityId,
            AssistPlayerEntityId = g.AssistPlayerEntityId,
            PreAssistPlayerEntityId = g.PreAssistPlayerEntityId
        }).ToList();

        foreach (var link in newLinks)
            await _goalRepository.AddGoalLinkAsync(link, ct);

        // 3) PreAssists = quantidade de links finais em que o jogador é o pré-assistente
        var removedIds = existingLinks.Select(l => l.Id).ToHashSet();
        var untouchedLinks = await _db.MatchGoalLinks
            .AsNoTracking()
            .Where(l => l.MatchId == matchId && l.PreAssistPlayerEntityId != null)
            .Select(l => new { l.Id, l.PreAssistPlayerEntityId })
            .ToListAsync(ct);

        var preAssistCount = newLinks
            .Where(l => l.PreAssistPlayerEntityId.HasValue)
            .Select(l => l.PreAssistPlayerEntityId!.Value)
            .Concat(untouchedLinks.Where(l => !removedIds.Contains(l.Id)).Select(l => l.PreAssistPlayerEntityId!.Value))
            .GroupBy(id => id)
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var mpItem in players.Where(x => affectedClubs.Contains(x.ClubId)))
            mpItem.PreAssists = (short)(preAssistCount.TryGetValue(mpItem.PlayerEntityId, out var n) ? n : 0);

        await _goalRepository.SaveChangesAsync(ct);
    }
}
