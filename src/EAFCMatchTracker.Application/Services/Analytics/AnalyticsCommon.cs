using System.Globalization;
using EAFCMatchTracker.Application.Dtos;
using EAFCMatchTracker.Application.Time;
using EAFCMatchTracker.Domain.Entities;
using EAFCMatchTracker.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace EAFCMatchTracker.Application.Services.Analytics;

/// <summary>Uma partida do nosso clube (projeção leve) com o adversário e os snapshots de SR/divisão.</summary>
internal sealed class MatchRow
{
    public long MatchId { get; init; }
    public DateTime Timestamp { get; init; }
    public int? GameVersionId { get; init; }
    public int Gf { get; init; }
    public int Ga { get; init; }
    public long OppClubId { get; init; }
    public string OppName { get; init; } = "";
    public string? OppCrest { get; init; }
    public string? OppCustomCrest { get; init; }

    /// <summary>SR do nosso clube após a partida (snapshot de OverallStats), quando conhecido.</summary>
    public int? OurSr { get; init; }
    public int? OppSr { get; init; }
    public int? OurDivision { get; init; }
    public int? OurPromotions { get; init; }
    public int? OurRelegations { get; init; }

    public int OurPlayers { get; set; }
    public int OppPlayers { get; set; }

    public string Result => Gf > Ga ? "W" : Gf < Ga ? "L" : "D";
    public int Points => Gf > Ga ? 3 : Gf == Ga ? 1 : 0;
}

internal sealed record PlayerRow(
    long MatchId, long PlayerId, string? ProName, string Pos, int Goals, int Assists, int PreAssists,
    double Rating, bool Mom, int RedCards);

internal sealed record SnapshotRow(
    long Id, long MatchId, long ClubId, string? SkillRating, int? CurrentDivision, string? Promotions, string? Relegations);

internal sealed record GoalLinkRow(long Id, long MatchId, long ScorerId, long? AssistId, long? PreAssistId);

[Flags]
internal enum DataParts { None = 0, Snapshots = 1, Players = 2, GoalLinks = 4, All = 7 }

internal sealed class ClubInfo
{
    public string TimeZoneId { get; init; } = "America/Sao_Paulo";
    public TimeZoneInfo Zone { get; init; } = TimeZoneInfo.Utc;
    public string Name { get; init; } = "";
}

internal sealed class ClubDataset
{
    public List<MatchRow> Matches { get; init; } = new();
    public Dictionary<long, List<PlayerRow>> PlayersByMatch { get; init; } = new();
    public Dictionary<long, string> Names { get; init; } = new();
    public List<GoalLinkRow> Goals { get; init; } = new();

    public string NameOf(long playerId) => Names.TryGetValue(playerId, out var n) ? n : $"Jogador {playerId}";
}

internal static class StatsUtil
{
    public const int MaxRangeYears = 5;

    public static double Round2(double v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    public static double Pct(int part, int total) => total == 0 ? 0 : Round2(part * 100.0 / total);

    /// <summary>Converte "1450", "1.450" ou "1,450" (cultura invariante); valores &lt;= 0 ou inválidos viram nulo.</summary>
    public static int? ParseSr(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (!double.TryParse(raw.Trim(), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var v))
            return null;
        if (double.IsNaN(v) || double.IsInfinity(v) || v <= 0 || v > 100_000) return null;
        return (int)Math.Round(v, MidpointRounding.AwayFromZero);
    }

    /// <summary>Contador acumulado (promoções/rebaixamentos); inválido ou negativo vira nulo.</summary>
    public static int? ParseCounter(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (!double.TryParse(raw.Trim(), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var v))
            return null;
        if (double.IsNaN(v) || double.IsInfinity(v) || v < 0 || v > 100_000) return null;
        return (int)Math.Round(v, MidpointRounding.AwayFromZero);
    }

    public static DateTime AsUtc(DateTime v) => BrazilTime.EnsureUtc(v);

    public static DateTime ToLocal(DateTime utc, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone);

    public static DateOnly LocalDate(DateTime utc, TimeZoneInfo zone) => DateOnly.FromDateTime(ToLocal(utc, zone));

    /// <summary>Instante UTC de 00:00 local do dia informado no fuso do clube.</summary>
    public static DateTime LocalDayStartUtc(DateOnly day, TimeZoneInfo zone)
    {
        var local = DateTime.SpecifyKind(day.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        try
        {
            return DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeToUtc(local, zone), DateTimeKind.Utc);
        }
        catch (ArgumentException)
        {
            return DateTime.SpecifyKind(local - zone.BaseUtcOffset, DateTimeKind.Utc);
        }
    }

    public static T Fill<T>(this T target, IReadOnlyCollection<MatchRow> rows) where T : LabStatsDto
    {
        var n = rows.Count;
        var w = rows.Count(r => r.Gf > r.Ga);
        var l = rows.Count(r => r.Gf < r.Ga);
        target.Matches = n;
        target.Wins = w;
        target.Losses = l;
        target.Draws = n - w - l;
        target.WinRatePct = Pct(w, n);
        target.PointsPerMatch = n == 0 ? 0 : Round2(rows.Sum(r => r.Points) / (double)n);
        target.GoalsForPerMatch = n == 0 ? 0 : Round2(rows.Sum(r => r.Gf) / (double)n);
        target.GoalsAgainstPerMatch = n == 0 ? 0 : Round2(rows.Sum(r => r.Ga) / (double)n);
        return target;
    }

    public static LabStatsDto Stats(IReadOnlyCollection<MatchRow> rows) => new LabStatsDto().Fill(rows);

    public static string Reliability(int smallestSample) => smallestSample < 5 ? "low" : smallestSample < 12 ? "medium" : "high";

    public static MatchRefDto MatchRef(MatchRow m) => new()
    {
        MatchId = m.MatchId,
        OpponentName = m.OppName,
        GoalsFor = m.Gf,
        GoalsAgainst = m.Ga
    };

    public static MatchRow? BiggestWin(IEnumerable<MatchRow> rows) => rows
        .Where(m => m.Gf > m.Ga)
        .OrderByDescending(m => m.Gf - m.Ga).ThenByDescending(m => m.Gf).ThenBy(m => m.Timestamp).ThenBy(m => m.MatchId)
        .FirstOrDefault();

    public static MatchRow? WorstLoss(IEnumerable<MatchRow> rows) => rows
        .Where(m => m.Gf < m.Ga)
        .OrderByDescending(m => m.Ga - m.Gf).ThenByDescending(m => m.Ga).ThenBy(m => m.Timestamp).ThenBy(m => m.MatchId)
        .FirstOrDefault();

    /// <summary>Agrupa por adversário; nome e escudo vêm da partida mais recente que os possui.</summary>
    public static List<OpponentWdlDto> OpponentRecords(IEnumerable<MatchRow> rows) => rows
        .GroupBy(m => m.OppClubId)
        .Select(g =>
        {
            var ordered = g.OrderByDescending(m => m.Timestamp).ThenByDescending(m => m.MatchId).ToList();
            return new OpponentWdlDto
            {
                OpponentClubId = g.Key,
                Name = ordered.Select(m => m.OppName).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)) ?? $"Clube {g.Key}",
                CrestAssetId = ordered.Select(m => m.OppCrest).FirstOrDefault(c => !string.IsNullOrWhiteSpace(c)),
                CustomCrestAssetId = ordered.Select(m => m.OppCustomCrest).FirstOrDefault(c => !string.IsNullOrWhiteSpace(c)),
                Matches = ordered.Count,
                Wins = ordered.Count(m => m.Gf > m.Ga),
                Draws = ordered.Count(m => m.Gf == m.Ga),
                Losses = ordered.Count(m => m.Gf < m.Ga)
            };
        })
        .ToList();
}

/// <summary>Leituras compartilhadas (somente leitura, AsNoTracking, projeções pequenas) das três análises.</summary>
internal sealed class ClubAnalyticsLoader
{
    private readonly EAFCContext _db;

    public ClubAnalyticsLoader(EAFCContext db) => _db = db;

    public async Task<ClubInfo> GetClubInfoAsync(long clubId, CancellationToken ct)
    {
        var tracked = await _db.TrackedClubs.AsNoTracking().FirstOrDefaultAsync(c => c.ClubId == clubId, ct);
        var tzId = string.IsNullOrWhiteSpace(tracked?.TimeZoneId) ? "America/Sao_Paulo" : tracked!.TimeZoneId;
        var latestName = await _db.MatchClubs.AsNoTracking()
            .Where(c => c.ClubId == clubId)
            .OrderByDescending(c => c.Match.Timestamp).ThenByDescending(c => c.MatchId)
            .Select(c => c.Details != null ? c.Details.Name : null)
            .FirstOrDefaultAsync(ct);
        return new ClubInfo
        {
            TimeZoneId = tzId,
            Zone = ClubSessionService.ResolveZone(tzId),
            Name = !string.IsNullOrWhiteSpace(latestName) ? latestName!
                : !string.IsNullOrWhiteSpace(tracked?.Name) ? tracked!.Name! : $"Clube {clubId}"
        };
    }

    /// <summary>Número da edição (ex.: 27) -> id da edição. Desconhecida: Known=false (resultado vazio).</summary>
    public async Task<(bool Known, int? VersionId, string? Name)> ResolveVersionAsync(int? gameVersion, CancellationToken ct)
    {
        if (gameVersion is null) return (true, null, null);
        var v = await _db.GameVersions.AsNoTracking()
            .Where(x => x.Version == gameVersion.Value)
            .Select(x => new { x.Id, x.Name })
            .FirstOrDefaultAsync(ct);
        return v is null ? (false, null, null) : (true, v.Id, v.Name);
    }

    /// <summary>
    /// Fingerprint barato do clube para a chave de cache: última partida, quantidade de partidas e de gols vinculados
    /// + a parametrização de sessão do admin (intervalo, fuso e fronteiras manuais), para que mudar o admin reflita na hora.
    /// </summary>
    public async Task<string> FingerprintAsync(long clubId, CancellationToken ct)
    {
        var q = _db.MatchClubs.AsNoTracking().Where(c => c.ClubId == clubId);
        var count = await q.CountAsync(ct);
        var latest = await q.MaxAsync(c => (DateTime?)c.Match.Timestamp, ct);
        var links = await _db.MatchGoalLinks.AsNoTracking().CountAsync(g => g.ClubId == clubId, ct);

        var settings = await _db.TrackedClubs.AsNoTracking()
            .Where(c => c.ClubId == clubId)
            .Select(c => new { c.SessionGapMinutes, c.TimeZoneId })
            .FirstOrDefaultAsync(ct);
        var boundaries = await _db.SessionBoundaries.AsNoTracking()
            .Where(b => b.ClubId == clubId)
            .Select(b => new { b.MatchId, b.StartNewSession })
            .ToListAsync(ct);
        // soma ponderada (independe da ordem): muda ao criar/remover/inverter qualquer fronteira
        long boundaryHash = 0;
        foreach (var b in boundaries) boundaryHash += b.MatchId * (b.StartNewSession ? 31 : 17);
        return $"{count}:{latest?.Ticks ?? 0}:{links}:g{settings?.SessionGapMinutes}:tz{settings?.TimeZoneId}:b{boundaries.Count}-{boundaryHash}";
    }

    public async Task<ClubDataset> LoadAsync(
        long clubId, int? versionId, DateTime? fromUtc, DateTime? toUtc, IReadOnlyCollection<long>? onlyIds,
        DataParts parts, CancellationToken ct)
    {
        var q = _db.MatchClubs.AsNoTracking().Where(c => c.ClubId == clubId);
        if (versionId.HasValue) q = q.Where(c => c.Match.GameVersionId == versionId.Value);
        if (fromUtc.HasValue) { var f = StatsUtil.AsUtc(fromUtc.Value); q = q.Where(c => c.Match.Timestamp >= f); }
        if (toUtc.HasValue) { var t = StatsUtil.AsUtc(toUtc.Value); q = q.Where(c => c.Match.Timestamp < t); }
        if (onlyIds is not null) { var only = onlyIds.ToList(); q = q.Where(c => only.Contains(c.MatchId)); }

        var ours = await q
            .Select(c => new
            {
                c.MatchId,
                c.Match.Timestamp,
                c.Match.GameVersionId,
                c.Goals,
                c.GoalsAgainst,
                c.CurrentDivision
            })
            .ToListAsync(ct);
        ours = ours.OrderBy(o => o.Timestamp).ThenBy(o => o.MatchId).ToList();
        var ids = ours.Select(o => o.MatchId).ToList();
        if (ids.Count == 0) return new ClubDataset();

        var opps = (await _db.MatchClubs.AsNoTracking()
                .Where(o => o.ClubId != clubId && ids.Contains(o.MatchId))
                .Select(o => new
                {
                    o.MatchId,
                    o.ClubId,
                    Name = o.Details != null ? o.Details.Name : null,
                    TeamId = o.Details != null ? (long?)o.Details.TeamId : null,
                    Crest = o.Details != null ? o.Details.CrestAssetId : null
                })
                .ToListAsync(ct))
            .GroupBy(o => o.MatchId)
            .ToDictionary(g => g.Key, g => g.First());

        // Um snapshot por (partida, clube): em duplicidade vale o mais recente (maior Id).
        var snapshots = !parts.HasFlag(DataParts.Snapshots)
            ? new Dictionary<(long MatchId, long ClubId), SnapshotRow>()
            : (await _db.OverallStats.AsNoTracking()
                    .Where(o => o.MatchId != null && ids.Contains(o.MatchId.Value))
                    .Select(o => new SnapshotRow(o.Id, o.MatchId!.Value, o.ClubId, o.SkillRating, o.CurrentDivision, o.Promotions, o.Relegations))
                    .ToListAsync(ct))
                .GroupBy(o => (o.MatchId, o.ClubId))
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Id).First());

        var matches = new List<MatchRow>(ours.Count);
        foreach (var o in ours)
        {
            opps.TryGetValue(o.MatchId, out var opp);
            snapshots.TryGetValue((o.MatchId, clubId), out var mine);
            var oppSnap = opp is not null && snapshots.TryGetValue((o.MatchId, opp.ClubId), out var s) ? s : null;
            matches.Add(new MatchRow
            {
                MatchId = o.MatchId,
                Timestamp = StatsUtil.AsUtc(o.Timestamp),
                GameVersionId = o.GameVersionId,
                Gf = o.Goals,
                Ga = o.GoalsAgainst,
                OppClubId = opp?.ClubId ?? 0,
                OppName = !string.IsNullOrWhiteSpace(opp?.Name) ? opp!.Name! : (opp is null ? "Adversário" : $"Clube {opp.ClubId}"),
                OppCrest = opp?.TeamId is > 0 ? opp.TeamId.Value.ToString(CultureInfo.InvariantCulture) : null,
                OppCustomCrest = string.IsNullOrWhiteSpace(opp?.Crest) ? null : opp!.Crest,
                OurSr = StatsUtil.ParseSr(mine?.SkillRating),
                OppSr = StatsUtil.ParseSr(oppSnap?.SkillRating),
                OurDivision = mine?.CurrentDivision ?? o.CurrentDivision,
                OurPromotions = StatsUtil.ParseCounter(mine?.Promotions),
                OurRelegations = StatsUtil.ParseCounter(mine?.Relegations)
            });
        }

        if (!parts.HasFlag(DataParts.Players)) return new ClubDataset { Matches = matches };

        var playerRows = (await _db.MatchPlayers.AsNoTracking()
                .Where(mp => mp.ClubId == clubId && ids.Contains(mp.MatchId))
                .Select(mp => new
                {
                    mp.MatchId, mp.PlayerEntityId, mp.ProName, mp.Pos, mp.Goals, mp.Assists, mp.PreAssists,
                    mp.Rating, mp.Mom, mp.Redcards
                })
                .ToListAsync(ct))
            .Select(x => new PlayerRow(x.MatchId, x.PlayerEntityId, x.ProName, x.Pos ?? "", x.Goals, x.Assists,
                x.PreAssists, double.IsNaN(x.Rating) ? 0 : x.Rating, x.Mom, x.Redcards))
            .ToList();
        var playersByMatch = playerRows.GroupBy(p => p.MatchId).ToDictionary(g => g.Key, g => g.ToList());

        var oppCounts = (await _db.MatchPlayers.AsNoTracking()
                .Where(mp => mp.ClubId != clubId && ids.Contains(mp.MatchId))
                .GroupBy(mp => mp.MatchId)
                .Select(g => new { MatchId = g.Key, N = g.Count() })
                .ToListAsync(ct))
            .ToDictionary(x => x.MatchId, x => x.N);

        foreach (var m in matches)
        {
            m.OurPlayers = playersByMatch.TryGetValue(m.MatchId, out var ps) ? ps.Count : 0;
            m.OppPlayers = oppCounts.TryGetValue(m.MatchId, out var n) ? n : 0;
        }

        var goals = new List<GoalLinkRow>();
        if (parts.HasFlag(DataParts.GoalLinks))
        {
            goals = (await _db.MatchGoalLinks.AsNoTracking()
                    .Where(g => g.ClubId == clubId && ids.Contains(g.MatchId))
                    .OrderBy(g => g.Id)
                    .Select(g => new { g.Id, g.MatchId, g.ScorerPlayerEntityId, g.AssistPlayerEntityId, g.PreAssistPlayerEntityId })
                    .ToListAsync(ct))
                .Select(g => new GoalLinkRow(g.Id, g.MatchId, g.ScorerPlayerEntityId, g.AssistPlayerEntityId, g.PreAssistPlayerEntityId))
                .ToList();
        }

        var playerIds = playerRows.Select(p => p.PlayerId)
            .Concat(goals.SelectMany(g => new[] { (long?)g.ScorerId, g.AssistId, g.PreAssistId }.Where(x => x.HasValue).Select(x => x!.Value)))
            .Distinct().ToList();
        var names = new Dictionary<long, string>();
        if (playerIds.Count > 0)
        {
            var dbNames = await _db.Players.AsNoTracking()
                .Where(p => playerIds.Contains(p.Id))
                .Select(p => new { p.Id, p.Playername })
                .ToListAsync(ct);
            foreach (var p in dbNames)
                if (!string.IsNullOrWhiteSpace(p.Playername)) names[p.Id] = p.Playername.Trim();
            foreach (var r in playerRows.Where(r => !names.ContainsKey(r.PlayerId) && !string.IsNullOrWhiteSpace(r.ProName)))
                names[r.PlayerId] = r.ProName!.Trim();
        }

        return new ClubDataset { Matches = matches, PlayersByMatch = playersByMatch, Names = names, Goals = goals };
    }
}

/// <summary>Cache de 60 s por clube + parâmetros + fingerprint do clube (última partida, contagens).</summary>
internal static class AnalyticsCache
{
    public static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

    public static async Task<T> GetOrCreateAsync<T>(
        IMemoryCache cache, ClubAnalyticsLoader loader, string kind, long clubId, string parameters,
        Func<Task<T>> factory, CancellationToken ct) where T : class
    {
        var key = $"analytics:{kind}:{clubId}:{parameters}:{await loader.FingerprintAsync(clubId, ct)}";
        if (cache.TryGetValue(key, out T? hit) && hit is not null) return hit;
        var value = await factory();
        cache.Set(key, value, Ttl);
        return value;
    }
}
