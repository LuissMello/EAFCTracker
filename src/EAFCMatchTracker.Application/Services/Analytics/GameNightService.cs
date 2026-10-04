using EAFCMatchTracker.Application.Dtos;
using EAFCMatchTracker.Application.Interfaces.Services;
using EAFCMatchTracker.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace EAFCMatchTracker.Application.Services.Analytics;

/// <summary>
/// "Noite de jogo": uma sessão do clube (ClubSessionService é a única fonte de sessões) com placar, destaques,
/// jogadores, adversários e cadeias de gols. Somente leitura.
/// </summary>
public sealed class GameNightService : IGameNightService
{
    private readonly EAFCContext _db;
    private readonly ClubSessionService _sessions;
    private readonly IMemoryCache _cache;
    private readonly ClubAnalyticsLoader _loader;

    public GameNightService(EAFCContext db, ClubSessionService sessions, IMemoryCache cache)
    {
        _db = db;
        _sessions = sessions;
        _cache = cache;
        _loader = new ClubAnalyticsLoader(db);
    }

    private sealed record Night(ClubSession Session, List<MatchRow> Matches)
    {
        public DateTime Start => Matches[0].Timestamp;
        public DateTime End => Matches[^1].Timestamp;
    }

    public Task<GameNightListDto> GetNightsAsync(long clubId, int? gameVersion, CancellationToken ct) =>
        AnalyticsCache.GetOrCreateAsync(_cache, _loader, "nights", clubId, $"v={gameVersion}", async () =>
        {
            var info = await _loader.GetClubInfoAsync(clubId, ct);
            var result = new GameNightListDto { ClubId = clubId, TimeZoneId = info.TimeZoneId };
            var nights = await LoadNightsAsync(clubId, gameVersion, ct);
            result.Nights = nights.Select(n => new GameNightSummaryDto
            {
                SessionId = n.Session.Id,
                Date = n.Session.Date,
                StartedAtUtc = n.Start,
                EndedAtUtc = n.End,
                Matches = n.Matches.Count,
                Wins = n.Matches.Count(m => m.Gf > m.Ga),
                Draws = n.Matches.Count(m => m.Gf == m.Ga),
                Losses = n.Matches.Count(m => m.Gf < m.Ga),
                GoalsFor = n.Matches.Sum(m => m.Gf),
                GoalsAgainst = n.Matches.Sum(m => m.Ga)
            }).ToList();
            return result;
        }, ct);

    public async Task<GameNightDetailDto?> GetNightAsync(long clubId, long sessionId, int? gameVersion, CancellationToken ct)
    {
        var key = $"s={sessionId}:v={gameVersion}";
        var fingerprint = await _loader.FingerprintAsync(clubId, ct);
        var cacheKey = $"analytics:night:{clubId}:{key}:{fingerprint}";
        if (_cache.TryGetValue(cacheKey, out GameNightDetailDto? hit) && hit is not null) return hit;

        var detail = await BuildDetailAsync(clubId, sessionId, gameVersion, ct);
        if (detail is not null) _cache.Set(cacheKey, detail, AnalyticsCache.Ttl);
        return detail;
    }

    /// <summary>Sessões do clube restritas às partidas da edição pedida; sessões sem partidas na edição somem.</summary>
    private async Task<List<Night>> LoadNightsAsync(long clubId, int? gameVersion, CancellationToken ct)
    {
        var (known, versionId, _) = await _loader.ResolveVersionAsync(gameVersion, ct);
        if (!known) return new List<Night>();

        var data = await _loader.LoadAsync(clubId, versionId, null, null, null, DataParts.None, ct);
        var byId = data.Matches.ToDictionary(m => m.MatchId);
        var sessions = await _sessions.GetForClubAsync(clubId, ct);

        var nights = new List<Night>();
        foreach (var s in sessions)
        {
            var ms = s.MatchIds.Where(byId.ContainsKey).Select(id => byId[id]).ToList();
            if (ms.Count > 0) nights.Add(new Night(s, ms));
        }
        return nights;
    }

    private async Task<GameNightDetailDto?> BuildDetailAsync(long clubId, long sessionId, int? gameVersion, CancellationToken ct)
    {
        var nights = await LoadNightsAsync(clubId, gameVersion, ct);
        var idx = nights.FindIndex(n => n.Session.Id == sessionId);
        if (idx < 0) return null;
        var night = nights[idx];

        var info = await _loader.GetClubInfoAsync(clubId, ct);
        var data = await _loader.LoadAsync(clubId, null, null, null, night.Matches.Select(m => m.MatchId).ToList(), DataParts.All, ct);
        var ms = data.Matches; // já ordenadas por horário
        if (ms.Count == 0) return null;
        var first = ms[0];
        var last = ms[^1];

        var wins = ms.Count(m => m.Gf > m.Ga);
        var draws = ms.Count(m => m.Gf == m.Ga);
        var losses = ms.Count(m => m.Gf < m.Ga);
        var gf = ms.Sum(m => m.Gf);
        var ga = ms.Sum(m => m.Ga);

        var (prevSr, prevDivision) = await PreviousSnapshotAsync(clubId, first, ct);

        var srAfter = ms.Where(m => m.OurSr.HasValue).Select(m => m.OurSr!.Value).ToList();
        int? srStart = prevSr ?? (srAfter.Count > 0 ? srAfter[0] : null);
        int? srEnd = srAfter.Count > 0 ? srAfter[^1] : null;
        var divisions = ms.Where(m => m.OurDivision.HasValue).Select(m => m.OurDivision!.Value).ToList();

        var playerRows = ms.SelectMany(m => data.PlayersByMatch.TryGetValue(m.MatchId, out var l) ? l : new List<PlayerRow>()).ToList();
        var players = BuildPlayers(playerRows, data);
        var goalsByMatch = data.Goals.ToLookup(g => g.MatchId);

        return new GameNightDetailDto
        {
            ClubId = clubId,
            ClubName = info.Name,
            SessionId = night.Session.Id,
            Date = night.Session.Date,
            TimeZoneId = info.TimeZoneId,
            StartedAtUtc = first.Timestamp,
            EndedAtUtc = last.Timestamp,
            DurationMinutes = (int)Math.Round((last.Timestamp - first.Timestamp).TotalMinutes),
            PrevSessionId = idx > 0 ? nights[idx - 1].Session.Id : null,
            NextSessionId = idx < nights.Count - 1 ? nights[idx + 1].Session.Id : null,
            Index = idx + 1,
            Total = nights.Count,
            Record = new GameNightRecordDto
            {
                Matches = ms.Count,
                Wins = wins,
                Draws = draws,
                Losses = losses,
                WinRatePct = StatsUtil.Pct(wins, ms.Count),
                GoalsFor = gf,
                GoalsAgainst = ga,
                GoalDiff = gf - ga,
                CleanSheets = ms.Count(m => m.Ga == 0)
            },
            SkillRating = new GameNightSkillRatingDto
            {
                Start = srStart,
                End = srEnd,
                Delta = srStart.HasValue && srEnd.HasValue ? srEnd - srStart : null
            },
            Division = new GameNightDivisionDto
            {
                Start = prevDivision ?? (divisions.Count > 0 ? divisions[0] : null),
                End = divisions.Count > 0 ? divisions[^1] : null
            },
            Highlights = BuildHighlights(ms, playerRows, players, data),
            Players = players,
            Opponents = StatsUtil.OpponentRecords(ms).OrderByDescending(o => o.Matches).ThenBy(o => o.Name).ToList(),
            Matches = ms.Select(m => new GameNightMatchDto
            {
                MatchId = m.MatchId,
                Timestamp = m.Timestamp,
                OpponentClubId = m.OppClubId,
                OpponentName = m.OppName,
                CrestAssetId = m.OppCrest,
                CustomCrestAssetId = m.OppCustomCrest,
                GoalsFor = m.Gf,
                GoalsAgainst = m.Ga,
                Result = m.Result,
                OurPlayersCount = m.OurPlayers,
                OpponentPlayersCount = m.OppPlayers,
                SkillRatingAfter = m.OurSr,
                Goals = goalsByMatch[m.MatchId].Select(g => new GameNightGoalDto
                {
                    ScorerName = data.NameOf(g.ScorerId),
                    AssistName = g.AssistId.HasValue ? data.NameOf(g.AssistId.Value) : null,
                    PreAssistName = g.PreAssistId.HasValue ? data.NameOf(g.PreAssistId.Value) : null
                }).ToList()
            }).ToList()
        };
    }

    /// <summary>
    /// SR/divisão logo antes da noite: snapshot da última partida anterior do clube NA MESMA edição que tenha o
    /// dado. (Cada snapshot é tirado depois da partida, então o da partida anterior é o "antes" da primeira.)
    /// </summary>
    private async Task<(int? Sr, int? Division)> PreviousSnapshotAsync(long clubId, MatchRow first, CancellationToken ct)
    {
        var firstTs = first.Timestamp;
        var version = first.GameVersionId;
        var rows = await (
                from o in _db.OverallStats.AsNoTracking()
                join m in _db.Matches.AsNoTracking() on o.MatchId equals (long?)m.MatchId
                where o.ClubId == clubId && m.Timestamp < firstTs && m.GameVersionId == version
                orderby m.Timestamp descending, m.MatchId descending
                select new { o.SkillRating, o.CurrentDivision })
            .Take(30)
            .ToListAsync(ct);

        int? sr = rows.Select(r => StatsUtil.ParseSr(r.SkillRating)).FirstOrDefault(v => v.HasValue);
        int? div = rows.Select(r => r.CurrentDivision).FirstOrDefault(v => v.HasValue);
        return (sr, div);
    }

    private sealed class PlayerAgg
    {
        public long Id { get; init; }
        public string Name { get; init; } = "";
        public string Position { get; set; } = "";
        public int Matches { get; set; }
        public int RatedMatches { get; set; }
        public double RatingSum { get; set; }
        public int Goals { get; set; }
        public int Assists { get; set; }
        public int PreAssists { get; set; }
        public int Motm { get; set; }
        public int RedCards { get; set; }
        public double AvgRating => RatedMatches == 0 ? 0 : StatsUtil.Round2(RatingSum / RatedMatches);
    }

    private static List<PlayerAgg> Aggregate(List<PlayerRow> rows, ClubDataset data) => rows
        .GroupBy(r => r.PlayerId)
        .Select(g =>
        {
            var rated = g.Where(r => r.Rating > 0).ToList();
            // Posição mais usada na noite; em empate vale a mais recente.
            var pos = g.Select((r, i) => (r.Pos, i)).Where(x => !string.IsNullOrWhiteSpace(x.Pos))
                .GroupBy(x => x.Pos)
                .OrderByDescending(x => x.Count()).ThenByDescending(x => x.Max(y => y.i))
                .Select(x => x.Key).FirstOrDefault() ?? "";
            return new PlayerAgg
            {
                Id = g.Key,
                Name = data.NameOf(g.Key),
                Position = pos,
                Matches = g.Count(),
                RatedMatches = rated.Count,
                RatingSum = rated.Sum(r => r.Rating),
                Goals = g.Sum(r => r.Goals),
                Assists = g.Sum(r => r.Assists),
                PreAssists = g.Sum(r => r.PreAssists),
                Motm = g.Count(r => r.Mom),
                RedCards = g.Sum(r => r.RedCards)
            };
        })
        .ToList();

    private static List<GameNightPlayerDto> BuildPlayers(List<PlayerRow> rows, ClubDataset data) => Aggregate(rows, data)
        .OrderByDescending(p => p.Goals).ThenByDescending(p => p.Assists).ThenByDescending(p => p.AvgRating)
        .ThenByDescending(p => p.Matches).ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
        .Select(p => new GameNightPlayerDto
        {
            PlayerEntityId = p.Id,
            Name = p.Name,
            Position = p.Position,
            Matches = p.Matches,
            Goals = p.Goals,
            Assists = p.Assists,
            PreAssists = p.PreAssists,
            AvgRating = p.AvgRating,
            Motm = p.Motm,
            RedCards = p.RedCards
        })
        .ToList();

    private static GameNightHighlightsDto BuildHighlights(
        List<MatchRow> ms, List<PlayerRow> rows, List<GameNightPlayerDto> players, ClubDataset data)
    {
        var biggestWin = StatsUtil.BiggestWin(ms);
        var worstLoss = StatsUtil.WorstLoss(ms);

        var topScorer = players.Where(p => p.Goals > 0)
            .OrderByDescending(p => p.Goals).ThenByDescending(p => p.Assists).ThenBy(p => p.Matches).ThenBy(p => p.Name).FirstOrDefault();
        var topAssister = players.Where(p => p.Assists > 0)
            .OrderByDescending(p => p.Assists).ThenByDescending(p => p.Goals).ThenBy(p => p.Matches).ThenBy(p => p.Name).FirstOrDefault();
        var motm = players.Where(p => p.Motm > 0)
            .OrderByDescending(p => p.Motm).ThenByDescending(p => p.Goals).ThenBy(p => p.Name).FirstOrDefault();

        // Melhor nota: exige ter jogado ao menos metade das partidas da noite (evita 1 jogo isolado vencer);
        // sem ninguém elegível, vale quem tem ao menos uma nota.
        var aggs = Aggregate(rows, data).Where(a => a.RatedMatches > 0).ToList();
        var threshold = (ms.Count + 1) / 2;
        var pool = aggs.Where(a => a.RatedMatches >= threshold).ToList();
        if (pool.Count == 0) pool = aggs;
        var bestRated = pool.OrderByDescending(a => a.AvgRating).ThenByDescending(a => a.RatedMatches).ThenBy(a => a.Name).FirstOrDefault();

        return new GameNightHighlightsDto
        {
            BiggestWin = biggestWin is null ? null : StatsUtil.MatchRef(biggestWin),
            WorstLoss = worstLoss is null ? null : StatsUtil.MatchRef(worstLoss),
            TopScorer = topScorer is null ? null : new PlayerGoalsDto { PlayerEntityId = topScorer.PlayerEntityId, Name = topScorer.Name, Goals = topScorer.Goals },
            TopAssister = topAssister is null ? null : new PlayerAssistsDto { PlayerEntityId = topAssister.PlayerEntityId, Name = topAssister.Name, Assists = topAssister.Assists },
            BestRated = bestRated is null ? null : new PlayerRatingDto { PlayerEntityId = bestRated.Id, Name = bestRated.Name, AvgRating = bestRated.AvgRating, Matches = bestRated.RatedMatches },
            ManOfTheMatch = motm is null ? null : new PlayerCountDto { PlayerEntityId = motm.PlayerEntityId, Name = motm.Name, Count = motm.Motm },
            HatTricks = rows.Where(r => r.Goals >= 3)
                .Select(r => new NightHatTrickDto { PlayerEntityId = r.PlayerId, Name = data.NameOf(r.PlayerId), MatchId = r.MatchId })
                .OrderBy(h => ms.FindIndex(m => m.MatchId == h.MatchId)).ThenBy(h => h.Name).ToList(),
            RedCards = rows.Sum(r => r.RedCards)
        };
    }
}
