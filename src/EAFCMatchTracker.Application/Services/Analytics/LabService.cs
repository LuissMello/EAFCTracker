using EAFCMatchTracker.Application.Dtos;
using EAFCMatchTracker.Application.Interfaces.Services;
using EAFCMatchTracker.Infrastructure.Data;
using Microsoft.Extensions.Caching.Memory;

namespace EAFCMatchTracker.Application.Services.Analytics;

/// <summary>
/// Laboratório "Com e Sem": compara os resultados do clube com/sem cada jogador, por contexto (dia, hora, posição
/// na noite, tamanho dos elencos, força do adversário) e por dupla. Correlação, não causa: tudo é descritivo.
/// </summary>
public sealed class LabService : ILabService
{
    public const int MinMatchesFloor = 1;
    public const int MinMatchesCeiling = 30;

    /// <summary>Diferença de SR (adversário − nosso) a partir da qual o adversário é "mais forte" (e, negativa, "mais fraco").</summary>
    public const int OpponentStrengthGap = 60;

    public const string BandWeaker = "mais fraco";
    public const string BandSimilar = "parecido";
    public const string BandStronger = "mais forte";

    /// <summary>A posição na noite agrupa "8 ou mais" na posição 8.</summary>
    public const int MaxSessionPosition = 8;

    /// <summary>Teto de jogadores considerados na enumeração de duplas (C(24,2) = 276 pares no máximo).</summary>
    public const int MaxDuoPlayers = 24;

    public const int MaxDuosPerList = 8;

    private readonly ClubSessionService _sessions;
    private readonly IMemoryCache _cache;
    private readonly ClubAnalyticsLoader _loader;

    public LabService(EAFCContext db, ClubSessionService sessions, IMemoryCache cache)
    {
        _sessions = sessions;
        _cache = cache;
        _loader = new ClubAnalyticsLoader(db);
    }

    private static int ClampMin(int minMatches) => Math.Clamp(minMatches, MinMatchesFloor, MinMatchesCeiling);

    private async Task<(ClubInfo Info, ClubDataset Data)> LoadAsync(
        long clubId, DateOnly? from, DateOnly? to, int? gameVersion, DataParts parts, CancellationToken ct)
    {
        var info = await _loader.GetClubInfoAsync(clubId, ct);
        var (known, versionId, _) = await _loader.ResolveVersionAsync(gameVersion, ct);
        if (!known) return (info, new ClubDataset());

        DateTime? fromUtc = from.HasValue ? StatsUtil.LocalDayStartUtc(from.Value, info.Zone) : null;
        DateTime? toUtc = to.HasValue ? StatsUtil.LocalDayStartUtc(to.Value.AddDays(1), info.Zone) : null;
        return (info, await _loader.LoadAsync(clubId, versionId, fromUtc, toUtc, null, parts, ct));
    }

    // ------------------------------------------------------------------------------------------ player impact

    public Task<LabPlayerImpactDto> GetPlayerImpactAsync(
        long clubId, DateOnly? from, DateOnly? to, int? gameVersion, int minMatches, CancellationToken ct)
    {
        minMatches = ClampMin(minMatches);
        return AnalyticsCache.GetOrCreateAsync(_cache, _loader, "lab-impact", clubId,
            $"{from:yyyy-MM-dd}:{to:yyyy-MM-dd}:v={gameVersion}:m={minMatches}", async () =>
            {
                var (_, data) = await LoadAsync(clubId, from, to, gameVersion, DataParts.Players, ct);
                return BuildPlayerImpact(clubId, from, to, minMatches, data);
            }, ct);
    }

    internal static LabPlayerImpactDto BuildPlayerImpact(long clubId, DateOnly? from, DateOnly? to, int minMatches, ClubDataset data)
    {
        var rows = data.Matches;
        var result = new LabPlayerImpactDto
        {
            ClubId = clubId,
            From = from,
            To = to,
            TotalMatches = rows.Count,
            Baseline = StatsUtil.Stats(rows)
        };

        var played = PlayedMatchIds(data);
        foreach (var (playerId, ids) in played.Where(p => p.Value.Count >= minMatches))
        {
            var withRows = rows.Where(m => ids.Contains(m.MatchId)).ToList();
            var withoutRows = rows.Where(m => !ids.Contains(m.MatchId)).ToList();
            var with = StatsUtil.Stats(withRows);
            var without = withoutRows.Count > 0 ? StatsUtil.Stats(withoutRows) : null;
            var smallest = Math.Min(with.Matches, withoutRows.Count);

            string? note = null;
            var reliability = StatsUtil.Reliability(smallest);
            if (withoutRows.Count == 0)
                note = "Jogou todas as partidas do período; não há jogos sem este jogador para comparar.";
            else if (reliability == "low")
                note = withoutRows.Count <= with.Matches
                    ? $"Poucos jogos sem este jogador ({withoutRows.Count}); leia com cautela."
                    : $"Poucos jogos com este jogador ({with.Matches}); leia com cautela.";
            else if (reliability == "medium")
                note = $"Amostra moderada (menor lado: {smallest} jogos); trate como indício, não como conclusão.";

            result.Players.Add(new LabPlayerImpactItemDto
            {
                PlayerEntityId = playerId,
                Name = data.NameOf(playerId),
                With = with,
                Without = without,
                Delta = without is null ? null : new LabPlayerDeltaDto
                {
                    WinRatePct = StatsUtil.Round2(WinRate(withRows) - WinRate(withoutRows)),
                    PointsPerMatch = StatsUtil.Round2(PointsPerMatch(withRows) - PointsPerMatch(withoutRows)),
                    GoalDiffPerMatch = StatsUtil.Round2(GoalDiffPerMatch(withRows) - GoalDiffPerMatch(withoutRows))
                },
                Reliability = reliability,
                Note = note
            });
        }

        result.Players = result.Players
            .OrderBy(p => p.Delta is null ? 1 : 0)
            .ThenByDescending(p => p.Delta?.PointsPerMatch ?? 0)
            .ThenByDescending(p => p.With.Matches)
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return result;
    }

    private static double WinRate(List<MatchRow> r) => r.Count == 0 ? 0 : r.Count(m => m.Gf > m.Ga) * 100.0 / r.Count;
    private static double PointsPerMatch(List<MatchRow> r) => r.Count == 0 ? 0 : r.Sum(m => m.Points) / (double)r.Count;
    private static double GoalDiffPerMatch(List<MatchRow> r) => r.Count == 0 ? 0 : r.Sum(m => m.Gf - m.Ga) / (double)r.Count;

    internal static Dictionary<long, HashSet<long>> PlayedMatchIds(ClubDataset data)
    {
        var played = new Dictionary<long, HashSet<long>>();
        foreach (var (matchId, players) in data.PlayersByMatch)
            foreach (var p in players)
            {
                if (!played.TryGetValue(p.PlayerId, out var set)) played[p.PlayerId] = set = new HashSet<long>();
                set.Add(matchId);
            }
        return played;
    }

    // ------------------------------------------------------------------------------------------ context

    public Task<LabContextDto> GetContextAsync(long clubId, DateOnly? from, DateOnly? to, int? gameVersion, CancellationToken ct) =>
        AnalyticsCache.GetOrCreateAsync(_cache, _loader, "lab-context", clubId,
            $"{from:yyyy-MM-dd}:{to:yyyy-MM-dd}:v={gameVersion}", async () =>
            {
                var (info, data) = await LoadAsync(clubId, from, to, gameVersion, DataParts.Snapshots | DataParts.Players, ct);
                var sessions = data.Matches.Count > 0 ? await _sessions.GetForClubAsync(clubId, ct) : new List<ClubSession>();
                return BuildContext(clubId, info, data, sessions);
            }, ct);

    internal static LabContextDto BuildContext(long clubId, ClubInfo info, ClubDataset data, List<ClubSession> sessions)
    {
        var rows = data.Matches;
        var positions = new Dictionary<long, int>();
        foreach (var s in sessions)
            for (var i = 0; i < s.MatchIds.Count; i++) positions[s.MatchIds[i]] = i + 1;

        var local = rows.ToDictionary(m => m.MatchId, m => StatsUtil.ToLocal(m.Timestamp, info.Zone));

        var result = new LabContextDto { ClubId = clubId, TimeZoneId = info.TimeZoneId, Baseline = StatsUtil.Stats(rows) };

        result.ByWeekday = rows.GroupBy(m => (int)local[m.MatchId].DayOfWeek).OrderBy(g => g.Key)
            .Select(g => new LabWeekdayBucketDto { Weekday = g.Key }.Fill(g.ToList())).ToList();
        result.ByHour = rows.GroupBy(m => local[m.MatchId].Hour).OrderBy(g => g.Key)
            .Select(g => new LabHourBucketDto { Hour = g.Key }.Fill(g.ToList())).ToList();
        result.BySessionPosition = rows.Where(m => positions.ContainsKey(m.MatchId))
            .GroupBy(m => Math.Min(positions[m.MatchId], MaxSessionPosition)).OrderBy(g => g.Key)
            .Select(g => new LabSessionPositionBucketDto { Position = g.Key }.Fill(g.ToList())).ToList();
        result.ByOurPlayers = rows.Where(m => m.OurPlayers > 0).GroupBy(m => m.OurPlayers).OrderBy(g => g.Key)
            .Select(g => new LabPlayersBucketDto { Players = g.Key }.Fill(g.ToList())).ToList();
        result.ByOpponentPlayers = rows.Where(m => m.OppPlayers > 0).GroupBy(m => m.OppPlayers).OrderBy(g => g.Key)
            .Select(g => new LabPlayersBucketDto { Players = g.Key }.Fill(g.ToList())).ToList();

        var withGap = rows.Where(m => m.OurSr.HasValue && m.OppSr.HasValue)
            .Select(m => (Row: m, Gap: m.OppSr!.Value - m.OurSr!.Value)).ToList();
        foreach (var band in new[] { BandWeaker, BandSimilar, BandStronger })
        {
            var inBand = withGap.Where(x => BandOf(x.Gap) == band).ToList();
            if (inBand.Count == 0) continue;
            result.ByOpponentStrength.Add(new LabOpponentStrengthBucketDto
            {
                Band = band,
                SrGapMin = inBand.Min(x => x.Gap),
                SrGapMax = inBand.Max(x => x.Gap)
            }.Fill(inBand.Select(x => x.Row).ToList()));
        }
        return result;
    }

    internal static string BandOf(int gap) =>
        gap <= -OpponentStrengthGap ? BandWeaker : gap >= OpponentStrengthGap ? BandStronger : BandSimilar;

    // ------------------------------------------------------------------------------------------ duos

    public Task<LabDuosDto> GetDuosAsync(
        long clubId, DateOnly? from, DateOnly? to, int? gameVersion, int minMatches, CancellationToken ct)
    {
        minMatches = ClampMin(minMatches);
        return AnalyticsCache.GetOrCreateAsync(_cache, _loader, "lab-duos", clubId,
            $"{from:yyyy-MM-dd}:{to:yyyy-MM-dd}:v={gameVersion}:m={minMatches}", async () =>
            {
                var (_, data) = await LoadAsync(clubId, from, to, gameVersion, DataParts.Players, ct);
                return BuildDuos(minMatches, data);
            }, ct);
    }

    /// <summary>Jogadores com amostra mínima, limitados aos <see cref="MaxDuoPlayers"/> mais frequentes (teto de pares).</summary>
    internal static HashSet<long> DuoPool(Dictionary<long, HashSet<long>> played, int minMatches) => played
        .Where(p => p.Value.Count >= minMatches)
        .OrderByDescending(p => p.Value.Count).ThenBy(p => p.Key)
        .Take(MaxDuoPlayers).Select(p => p.Key).ToHashSet();

    internal static LabDuosDto BuildDuos(int minMatches, ClubDataset data)
    {
        var rows = data.Matches;
        var played = PlayedMatchIds(data);

        var eligible = DuoPool(played, minMatches);

        var together = new Dictionary<(long A, long B), List<MatchRow>>();
        foreach (var m in rows)
        {
            if (!data.PlayersByMatch.TryGetValue(m.MatchId, out var ps)) continue;
            var present = ps.Select(p => p.PlayerId).Where(eligible.Contains).Distinct().OrderBy(id => id).ToList();
            for (var i = 0; i < present.Count; i++)
                for (var j = i + 1; j < present.Count; j++)
                {
                    var key = (present[i], present[j]);
                    if (!together.TryGetValue(key, out var list)) together[key] = list = new List<MatchRow>();
                    list.Add(m);
                }
        }

        var duos = new List<LabDuoDto>();
        foreach (var ((a, b), togetherRows) in together)
        {
            if (togetherRows.Count < minMatches) continue;
            var ids = togetherRows.Select(m => m.MatchId).ToHashSet();
            var apartRows = rows.Where(m => !ids.Contains(m.MatchId)).ToList();
            var apart = apartRows.Count > 0 ? StatsUtil.Stats(apartRows) : null;
            var stats = StatsUtil.Stats(togetherRows);
            duos.Add(new LabDuoDto
            {
                APlayerEntityId = a,
                AName = data.NameOf(a),
                BPlayerEntityId = b,
                BName = data.NameOf(b),
                Together = stats,
                Apart = apart,
                Delta = apart is null ? null : new LabDuoDeltaDto
                {
                    WinRatePct = StatsUtil.Round2(WinRate(togetherRows) - WinRate(apartRows)),
                    PointsPerMatch = StatsUtil.Round2(PointsPerMatch(togetherRows) - PointsPerMatch(apartRows))
                },
                Reliability = StatsUtil.Reliability(Math.Min(stats.Matches, apartRows.Count))
            });
        }

        // Sem jogos "separados" não há comparação: ficam fora dos rankings. Melhor = delta > 0; pior = delta < 0.
        var comparable = duos.Where(d => d.Delta is not null).ToList();
        return new LabDuosDto
        {
            Best = comparable.Where(d => d.Delta!.PointsPerMatch > 0)
                .OrderByDescending(d => d.Delta!.PointsPerMatch).ThenByDescending(d => d.Delta!.WinRatePct)
                .ThenByDescending(d => d.Together.Matches).ThenBy(d => d.AName).ThenBy(d => d.BName)
                .Take(MaxDuosPerList).ToList(),
            Worst = comparable.Where(d => d.Delta!.PointsPerMatch < 0)
                .OrderBy(d => d.Delta!.PointsPerMatch).ThenBy(d => d.Delta!.WinRatePct)
                .ThenByDescending(d => d.Together.Matches).ThenBy(d => d.AName).ThenBy(d => d.BName)
                .Take(MaxDuosPerList).ToList()
        };
    }
}
