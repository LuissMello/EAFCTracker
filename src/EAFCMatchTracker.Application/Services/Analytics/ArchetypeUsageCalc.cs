using System.Globalization;
using EAFCMatchTracker.Application.Dtos;
using EAFCMatchTracker.Domain.Entities;

namespace EAFCMatchTracker.Application.Services.Analytics;

/// <summary>Uma linha jogador×partida reduzida ao que o uso de arquétipos precisa (id 0 = sem dado).</summary>
internal readonly record struct ArchetypeSample(
    int ArchetypeId, DateTime Timestamp, long MatchId, double Rating, int? ProOverall, string? ProOverallStr,
    int Goals, int Assists)
{
    public static ArchetypeSample From(MatchPlayerEntity mp) => new(
        mp.Archetypeid, mp.Match?.Timestamp ?? DateTime.MinValue, mp.MatchId, double.IsNaN(mp.Rating) ? 0 : mp.Rating,
        mp.ProOverall, mp.ProOverallStr, mp.Goals, mp.Assists);

    /// <summary>ProOverall, ou ProOverallStr numérico; nulo/0 ignorados.</summary>
    public int? Overall => ArchetypeUsageCalc.OverallOf(ProOverall, ProOverallStr);
}

/// <summary>
/// Regras compartilhadas de "arquétipo usado": uso por arquétipo (jogos, %, nota, overall, gols, assistências, primeira e
/// última partida), principal (mais jogos; empate = o mais recente) e trocas reais (ignora partidas sem dado).
/// Puro: sem acesso a banco. As referências saem de <paramref name="resolve"/> (catálogo ou <see cref="ArchetypeRef.Unresolved"/>).
/// </summary>
internal static class ArchetypeUsageCalc
{
    public static int? OverallOf(int? proOverall, string? proOverallStr)
    {
        if (proOverall is > 0) return proOverall;
        return int.TryParse(proOverallStr?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) && v > 0 ? v : null;
    }

    public static ArchetypeRef Unresolved(int id) => ArchetypeRef.Unresolved(id);

    /// <summary>
    /// Uso por arquétipo (id &gt; 0). O denominador de <c>Pct</c> é o total de amostras recebidas (inclui as sem arquétipo).
    /// Ordem: mais jogos primeiro; empate: o mais recente; depois o menor id. O primeiro item é o "principal".
    /// </summary>
    public static List<ArchetypeUsage> Usages(IReadOnlyCollection<ArchetypeSample> samples, Func<int, ArchetypeRef> resolve)
    {
        var total = samples.Count;
        if (total == 0) return new List<ArchetypeUsage>();

        return samples.Where(s => s.ArchetypeId > 0)
            .GroupBy(s => s.ArchetypeId)
            .Select(g =>
            {
                var list = g.ToList();
                var rated = list.Where(s => s.Rating > 0).ToList();
                var overalls = list.Select(s => s.Overall).Where(v => v.HasValue).Select(v => (double)v!.Value).ToList();
                return new ArchetypeUsage
                {
                    Archetype = resolve(g.Key),
                    Matches = list.Count,
                    Pct = StatsUtil.Round2(list.Count * 100.0 / total),
                    AvgRating = rated.Count > 0 ? StatsUtil.Round2(rated.Average(s => s.Rating)) : null,
                    AvgProOverall = overalls.Count > 0 ? StatsUtil.Round2(overalls.Average()) : null,
                    Goals = list.Sum(s => s.Goals),
                    Assists = list.Sum(s => s.Assists),
                    FirstPlayedAt = list.Min(s => StatsUtil.AsUtc(s.Timestamp)),
                    LastPlayedAt = list.Max(s => StatsUtil.AsUtc(s.Timestamp))
                };
            })
            .OrderByDescending(u => u.Matches).ThenByDescending(u => u.LastPlayedAt).ThenBy(u => u.Archetype.Id)
            .ToList();
    }

    /// <summary>
    /// Trocas em ordem cronológica: só quando o id (&gt; 0) muda em relação ao último id conhecido; partidas sem dado
    /// (0) não contam como troca nem zeram o último arquétipo.
    /// </summary>
    public static List<ArchetypeChange> Changes(IEnumerable<ArchetypeSample> samples, Func<int, ArchetypeRef> resolve)
    {
        var changes = new List<ArchetypeChange>();
        var last = 0;
        foreach (var s in samples.OrderBy(s => s.Timestamp).ThenBy(s => s.MatchId))
        {
            if (s.ArchetypeId <= 0) continue;
            if (last != 0 && s.ArchetypeId != last)
                changes.Add(new ArchetypeChange
                {
                    At = StatsUtil.AsUtc(s.Timestamp),
                    MatchId = s.MatchId,
                    From = resolve(last),
                    To = resolve(s.ArchetypeId)
                });
            last = s.ArchetypeId;
        }
        return changes;
    }

    /// <summary>Amostras de uma lista de <see cref="PlayerRow"/> (timestamp vindo das partidas do conjunto de dados).</summary>
    public static List<ArchetypeSample> SamplesOf(IEnumerable<PlayerRow> rows, ClubDataset data)
    {
        var ts = data.Matches.ToDictionary(m => m.MatchId, m => m.Timestamp);
        return rows.Select(r => new ArchetypeSample(
            r.ArchetypeId, ts.TryGetValue(r.MatchId, out var t) ? t : DateTime.MinValue, r.MatchId, r.Rating,
            r.ProOverall, r.ProOverallStr, r.Goals, r.Assists)).ToList();
    }

    /// <summary>Grupo da posição ("ATAQUE"...); nulo para posição vazia (fora de qualquer filtro de posição).</summary>
    public static string? GroupOfPos(string? pos) => string.IsNullOrWhiteSpace(pos) ? null : CardScoring.PositionGroup(pos);

    /// <summary>Linhas que passam nos filtros (AND). Filtro nulo = não restringe.</summary>
    public static IEnumerable<T> Where<T>(IEnumerable<T> rows, Func<T, int> arch, Func<T, string?> pos, string? group, int? archetypeId)
    {
        if (group is not null) rows = rows.Where(r => GroupOfPos(pos(r)) == group);
        if (archetypeId.HasValue) rows = rows.Where(r => arch(r) == archetypeId.Value);
        return rows;
    }

    /// <summary>Opções do filtro de arquétipo: calculadas COM a posição e SEM o filtro de arquétipo.</summary>
    public static List<AvailableArchetypeDto> AvailableArchetypes<T>(
        IEnumerable<T> allRows, Func<T, int> arch, Func<T, long> player, Func<T, string?> pos, string? group, Func<int, ArchetypeRef> resolve) =>
        Where(allRows, arch, pos, group, null)
            .Where(r => arch(r) > 0)
            .GroupBy(arch)
            .Select(g => new AvailableArchetypeDto { Archetype = resolve(g.Key), Players = g.Select(player).Distinct().Count(), Matches = g.Count() })
            .OrderByDescending(a => a.Matches).ThenBy(a => a.Archetype.Id)
            .ToList();

    /// <summary>Opções do filtro de posição: calculadas sem nenhum dos dois filtros (posição vazia não conta).</summary>
    public static List<AvailablePositionGroupDto> AvailablePositionGroups<T>(IEnumerable<T> allRows, Func<T, string?> pos) =>
        allRows.Select(r => GroupOfPos(pos(r))).Where(g => g is not null)
            .GroupBy(g => g!)
            .Select(g => new AvailablePositionGroupDto { PositionGroup = g.Key, Matches = g.Count() })
            .OrderByDescending(g => g.Matches).ThenBy(g => Array.IndexOf(ArchetypeGroups.All, g.PositionGroup))
            .ToList();

    /// <summary>Principal = primeiro do <see cref="Usages"/>; nulo quando não há dado.</summary>
    public static ArchetypeRef? Principal(IReadOnlyList<ArchetypeUsage> usages) => usages.Count > 0 ? usages[0].Archetype : null;
}
