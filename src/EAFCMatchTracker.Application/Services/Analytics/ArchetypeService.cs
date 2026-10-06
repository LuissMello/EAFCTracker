using EAFCMatchTracker.Application.Dtos;
using EAFCMatchTracker.Application.Interfaces.Services;
using EAFCMatchTracker.Domain.Entities;
using EAFCMatchTracker.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace EAFCMatchTracker.Application.Services.Analytics;

/// <summary>
/// Arquétipos: catálogo público, resumo por clube (mesmas linhas jogador×partida das Cartas, sem desconectados) e
/// edição do catálogo no Admin. Leituras sem N+1: o resumo reaproveita o carregamento das Cartas e o Admin usa
/// consultas agregadas (GROUP BY) no banco.
/// </summary>
public sealed class ArchetypeService : IArchetypeService
{
    public const int MaxTopPlayers = 3;

    private readonly EAFCContext _db;
    private readonly IMemoryCache _cache;
    private readonly IArchetypeCatalog _catalog;
    private readonly ClubAnalyticsLoader _loader;

    public ArchetypeService(EAFCContext db, IMemoryCache cache, IArchetypeCatalog catalog)
    {
        _db = db;
        _cache = cache;
        _catalog = catalog;
        _loader = new ClubAnalyticsLoader(db);
    }

    public async Task<List<ArchetypeRef>> GetCatalogAsync(CancellationToken ct) => (await _catalog.GetAsync(ct)).All();

    // ------------------------------------------------------------------------------------------ summary

    public Task<ArchetypeSummaryDto> GetSummaryAsync(
        long clubId, DateOnly? from, DateOnly? to, int? gameVersion, CancellationToken ct) =>
        GetSummaryAsync(clubId, from, to, gameVersion, null, null, ct);

    public async Task<ArchetypeSummaryDto> GetSummaryAsync(
        long clubId, DateOnly? from, DateOnly? to, int? gameVersion, int? archetypeId, string? positionGroup, CancellationToken ct)
    {
        positionGroup = ArchetypeGroups.Normalize(positionGroup);
        var catalog = await _catalog.GetAsync(ct);
        return await AnalyticsCache.GetOrCreateAsync(_cache, _loader, "archetype-summary", clubId,
            $"{from:yyyy-MM-dd}:{to:yyyy-MM-dd}:v={gameVersion}:arq={archetypeId}:pos={positionGroup}:c{catalog.Version}", async () =>
            {
                var d = (await PlayerCardService.LoadCardDataAsync(_db, _loader, clubId, from, to, gameVersion, ct)).WithCatalog(catalog);
                return BuildSummary(clubId, from, to, gameVersion, d, archetypeId, positionGroup);
            }, ct);
    }

    internal static ArchetypeSummaryDto BuildSummary(
        long clubId, DateOnly? from, DateOnly? to, int? gameVersion, PlayerCardService.CardData d,
        int? archetypeId = null, string? positionGroup = null)
    {
        var available = PlayerCardService.AvailableArchetypes(d, positionGroup);
        var positions = ArchetypeUsageCalc.AvailablePositionGroups(d.UsageRows, r => r.Pos);
        d = d.WithFilter(archetypeId, positionGroup);
        var matchesById = d.Data.Matches.ToDictionary(m => m.MatchId);
        var rows = d.Rows;

        ArchetypeSample Sample(CardRow r) => new(
            r.ArchetypeId, matchesById.TryGetValue(r.MatchId, out var m) ? m.Timestamp : DateTime.MinValue, r.MatchId,
            r.Rating, r.ProOverall, r.ProOverallStr, r.Goals, r.Assists);
        double? Avg(IEnumerable<double> values) { var l = values.ToList(); return l.Count > 0 ? StatsUtil.Round2(l.Average()) : null; }
        double? RatingAvg(IEnumerable<CardRow> rs) => Avg(rs.Where(r => r.Rating > 0).Select(r => r.Rating));
        double? OverallAvg(IEnumerable<CardRow> rs) =>
            Avg(rs.Select(r => ArchetypeUsageCalc.OverallOf(r.ProOverall, r.ProOverallStr)).Where(v => v.HasValue).Select(v => (double)v!.Value));
        double? PctOrNull(int part, int total) => total > 0 ? StatsUtil.Round2(part * 100.0 / total) : null;

        var archetypeRows = rows.Where(r => r.ArchetypeId > 0).GroupBy(r => r.ArchetypeId)
            .Select(g =>
            {
                var list = g.ToList();
                var overalls = list.Select(r => ArchetypeUsageCalc.OverallOf(r.ProOverall, r.ProOverallStr))
                    .Where(v => v.HasValue).Select(v => v!.Value).ToList();
                var wins = list.Count(r => matchesById.TryGetValue(r.MatchId, out var m) && m.Gf > m.Ga);
                int shots = list.Sum(r => r.Shots), passAtt = list.Sum(r => r.PassAttempts), tackleAtt = list.Sum(r => r.TackleAttempts);
                return new ArchetypeSummaryRowDto
                {
                    Archetype = d.Catalog.RefOf(g.Key),
                    Matches = list.Count,
                    Players = list.Select(r => r.PlayerId).Distinct().Count(),
                    AvgRating = RatingAvg(list),
                    AvgProOverall = OverallAvg(list),
                    MinProOverall = overalls.Count > 0 ? overalls.Min() : null,
                    MaxProOverall = overalls.Count > 0 ? overalls.Max() : null,
                    GoalsPerMatch = StatsUtil.Round2(list.Sum(r => r.Goals) / (double)list.Count),
                    AssistsPerMatch = StatsUtil.Round2(list.Sum(r => r.Assists) / (double)list.Count),
                    WinPct = StatsUtil.Pct(wins, list.Count),
                    PassAccuracyPct = PctOrNull(list.Sum(r => r.PassesMade), passAtt),
                    ShotAccuracyPct = PctOrNull(list.Sum(r => r.Goals), shots),
                    TackleAccuracyPct = PctOrNull(list.Sum(r => r.TacklesMade), tackleAtt),
                    TopPlayers = list.GroupBy(r => r.PlayerId)
                        .Select(pg => new ArchetypeSummaryTopPlayerDto
                        {
                            PlayerEntityId = pg.Key,
                            Name = d.Data.NameOf(pg.Key),
                            Matches = pg.Count(),
                            AvgRating = RatingAvg(pg),
                            AvgProOverall = OverallAvg(pg)
                        })
                        .OrderByDescending(p => p.Matches).ThenByDescending(p => p.AvgRating ?? 0)
                        .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ThenBy(p => p.PlayerEntityId)
                        .Take(MaxTopPlayers).ToList()
                };
            })
            .OrderByDescending(r => r.Matches).ThenBy(r => r.Archetype.Id)
            .ToList();

        var byPlayer = rows.GroupBy(r => r.PlayerId)
            .Select(g =>
            {
                var samples = g.Select(Sample).ToList();
                return new ArchetypeSummaryPlayerDto
                {
                    PlayerEntityId = g.Key,
                    Name = d.Data.NameOf(g.Key),
                    Switches = ArchetypeUsageCalc.Changes(samples, d.Catalog.RefOf).Count,
                    Archetypes = ArchetypeUsageCalc.Usages(samples, d.Catalog.RefOf),
                    Total = samples.Count
                };
            })
            .Where(p => p.Archetypes.Count > 0)
            .OrderByDescending(p => p.Total).ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ThenBy(p => p.PlayerEntityId)
            .ToList();

        return new ArchetypeSummaryDto
        {
            ClubId = clubId,
            From = from,
            To = to,
            GameVersion = gameVersion,
            PositionGroup = positionGroup,
            ArchetypeId = archetypeId,
            AvailableArchetypes = available,
            AvailablePositionGroups = positions,
            TotalPlayerMatches = rows.Count,
            WithoutArchetype = rows.Count(r => r.ArchetypeId <= 0),
            Archetypes = archetypeRows,
            ByPlayer = byPlayer
        };
    }

    // ------------------------------------------------------------------------------------------ admin

    public async Task<List<AdminArchetypeDto>> GetAdminListAsync(CancellationToken ct) => await BuildAdminAsync(null, ct);

    private async Task<List<AdminArchetypeDto>> BuildAdminAsync(int? onlyId, CancellationToken ct)
    {
        var catalog = await _catalog.GetAsync(ct);

        IQueryable<MatchPlayerEntity> Q() => onlyId.HasValue
            ? _db.MatchPlayers.AsNoTracking().Where(mp => mp.Archetypeid == onlyId.Value)
            : _db.MatchPlayers.AsNoTracking().Where(mp => mp.Archetypeid > 0);

        // Consultas agregadas (pequenas): jogos por (arquétipo, jogador) e por (arquétipo, overall).
        var perPlayer = await Q().GroupBy(mp => new { mp.Archetypeid, mp.PlayerEntityId })
            .Select(g => new { g.Key.Archetypeid, g.Key.PlayerEntityId, N = g.Count() }).ToListAsync(ct);
        var overalls = await Q().GroupBy(mp => new { mp.Archetypeid, mp.ProOverall, mp.ProOverallStr })
            .Select(g => new { g.Key.Archetypeid, g.Key.ProOverall, g.Key.ProOverallStr, N = g.Count() }).ToListAsync(ct);

        var top = perPlayer.GroupBy(p => (int)p.Archetypeid)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.N).ThenBy(p => p.PlayerEntityId).Take(MaxTopPlayers).ToList());
        var topIds = top.Values.SelectMany(l => l).Select(p => p.PlayerEntityId).Distinct().ToList();
        var names = topIds.Count == 0
            ? new Dictionary<long, string>()
            : (await _db.Players.AsNoTracking().Where(p => topIds.Contains(p.Id)).Select(p => new { p.Id, p.Playername }).ToListAsync(ct))
                .ToDictionary(p => p.Id, p => p.Playername ?? "");

        var ids = onlyId.HasValue ? new List<int> { onlyId.Value } : catalog.All().Select(r => r.Id).ToList();
        ids = ids.Concat(perPlayer.Select(p => (int)p.Archetypeid)).Distinct().OrderBy(i => i).ToList();

        return ids.Select(id =>
        {
            var entry = catalog.Entry(id);
            var mine = perPlayer.Where(p => p.Archetypeid == id).ToList();
            var ov = overalls.Where(o => o.Archetypeid == id)
                .Select(o => (Value: ArchetypeUsageCalc.OverallOf(o.ProOverall, o.ProOverallStr), o.N))
                .Where(x => x.Value.HasValue).ToList();
            var ovCount = ov.Sum(x => x.N);
            return new AdminArchetypeDto
            {
                Id = id,
                Name = string.IsNullOrWhiteSpace(entry?.Name) ? null : entry!.Name,
                ShortName = string.IsNullOrWhiteSpace(entry?.ShortName) ? null : entry!.ShortName,
                PositionGroup = ArchetypeGroups.Normalize(entry?.PositionGroup),
                InferredPositionGroup = catalog.InferredGroup(id),
                Matches = mine.Sum(p => p.N),
                Players = mine.Count,
                AvgProOverall = ovCount > 0 ? StatsUtil.Round2(ov.Sum(x => (double)x.Value!.Value * x.N) / ovCount) : null,
                TopPlayers = top.TryGetValue(id, out var t)
                    ? t.Select(p => new AdminArchetypeTopPlayerDto
                    {
                        PlayerEntityId = p.PlayerEntityId,
                        Name = names.TryGetValue(p.PlayerEntityId, out var n) && n.Length > 0 ? n : $"Jogador {p.PlayerEntityId}",
                        Matches = p.N
                    }).ToList()
                    : new List<AdminArchetypeTopPlayerDto>(),
                UpdatedAt = catalog.IsDefault(id) || entry is null ? null : DateTime.SpecifyKind(entry.UpdatedAtUtc, DateTimeKind.Utc)
            };
        }).ToList();
    }

    /// <summary>
    /// Valida e normaliza o corpo do PUT: id 1..255; strings vazias -> nulo; nome ≤ 40, sigla ≤ 8; grupo (sem diferenciar
    /// maiúsculas) em ATAQUE/MEIO/DEFESA/GOLEIRO. Devolve a mensagem de erro (pt-BR) ou nulo se válido.
    /// </summary>
    public static string? Validate(int id, AdminArchetypeUpdateDto? body, out AdminArchetypeUpdateDto normalized)
    {
        normalized = new AdminArchetypeUpdateDto();
        if (id < 1 || id > 255) return "O id do arquétipo deve estar entre 1 e 255.";
        if (body is null) return "Corpo da requisição ausente.";

        static string? Trim(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        var name = Trim(body.Name);
        var shortName = Trim(body.ShortName);
        var groupRaw = Trim(body.PositionGroup);

        if (name is { Length: > PlayerArchetypeEntity.MaxNameLength })
            return $"O nome deve ter no máximo {PlayerArchetypeEntity.MaxNameLength} caracteres.";
        if (shortName is { Length: > PlayerArchetypeEntity.MaxShortNameLength })
            return $"A sigla deve ter no máximo {PlayerArchetypeEntity.MaxShortNameLength} caracteres.";
        string? group = null;
        if (groupRaw is not null)
        {
            group = ArchetypeGroups.Normalize(groupRaw);
            if (group is null) return $"positionGroup deve ser um de: {string.Join(", ", ArchetypeGroups.All)} (ou nulo).";
        }

        normalized = new AdminArchetypeUpdateDto { Name = name, ShortName = shortName, PositionGroup = group };
        return null;
    }

    public async Task<AdminArchetypeDto> UpdateAsync(int id, AdminArchetypeUpdateDto normalized, CancellationToken ct)
    {
        try
        {
            var entity = await _db.PlayerArchetypes.FirstOrDefaultAsync(a => a.Id == id, ct);
            if (entity is null)
            {
                entity = new PlayerArchetypeEntity { Id = (short)id };
                _db.PlayerArchetypes.Add(entity);
            }
            entity.Name = normalized.Name;
            entity.ShortName = normalized.ShortName;
            entity.PositionGroup = normalized.PositionGroup;
            entity.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException && ArchetypeCatalog.IsMissingTable(ex))
        {
            throw new ArchetypeTableMissingException(ex);
        }

        _catalog.Invalidate();
        return (await BuildAdminAsync(id, ct)).First();
    }
}
