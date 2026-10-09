using System.Globalization;
using System.Text;
using EAFCMatchTracker.Application.Dtos;
using EAFCMatchTracker.Application.Exceptions;
using EAFCMatchTracker.Application.Interfaces.Services;
using EAFCMatchTracker.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace EAFCMatchTracker.Application.Services;

public sealed class OpponentSearchService : IOpponentSearchService
{
    public const int MinQueryLength = 2;
    public const int DefaultLimit = 15;
    public const int MaxLimit = 20;
    public const int MaxQueryLength = 100;

    private static readonly TimeSpan HistoryTtl = TimeSpan.FromSeconds(60);

    private readonly EAFCContext _db;
    private readonly IEaClubSearchClient _ea;
    private readonly IMemoryCache _cache;

    public OpponentSearchService(EAFCContext db, IEaClubSearchClient ea, IMemoryCache cache)
    {
        _db = db;
        _ea = ea;
        _cache = cache;
    }

    internal sealed record HistoryOpponent(
        long ClubId, string Name, string NormalizedName, string? CrestAssetId, int? Division, int TimesFaced, DateTime LastFacedAt,
        string? CustomCrestAssetId = null);

    public async Task<OpponentSearchResponseDto> SearchAsync(string query, long? clubId, int? limit, CancellationToken ct)
    {
        var q = (query ?? string.Empty).Trim();
        if (q.Length < MinQueryLength)
            throw new DomainValidationException($"Digite ao menos {MinQueryLength} caracteres para buscar.");
        if (q.Length > MaxQueryLength)
            throw new DomainValidationException($"A busca deve ter no máximo {MaxQueryLength} caracteres.");

        var take = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);

        // A EA roda em paralelo com a consulta ao banco (contextos/objetos distintos)
        var eaTask = _ea.SearchAsync(q, ct);
        var history = clubId.HasValue ? await GetHistoryAsync(clubId.Value, ct) : new List<HistoryOpponent>();
        var ea = await eaTask;

        var normalizedQuery = Normalize(q);
        var tokens = normalizedQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var matched = history
            .Select(h => new { H = h, Rank = Rank(h.NormalizedName, normalizedQuery, tokens) })
            .Where(x => x.Rank >= 0)
            .OrderBy(x => x.Rank)
            .ThenByDescending(x => x.H.TimesFaced)
            .ThenByDescending(x => x.H.LastFacedAt)
            .Select(x => x.H)
            .ToList();

        var eaById = ea.Items
            .Where(i => !clubId.HasValue || i.ClubId != clubId.Value)
            .ToDictionary(i => i.ClubId);

        var results = new List<OpponentSearchItemDto>();
        var used = new HashSet<long>();

        foreach (var h in matched)
        {
            if (!used.Add(h.ClubId)) continue;
            eaById.TryGetValue(h.ClubId, out var e);
            results.Add(new OpponentSearchItemDto
            {
                ClubId = h.ClubId,
                Name = e?.Name ?? h.Name,
                CurrentDivision = e?.CurrentDivision ?? h.Division,
                Division = e?.CurrentDivision ?? h.Division,
                SkillRating = e?.SkillRating,
                TeamId = TeamIdOf(e?.CrestAssetId ?? h.CrestAssetId),
                ReputationTier = e?.ReputationTier,
                CrestAssetId = e?.CrestAssetId ?? h.CrestAssetId,
                CustomCrestAssetId = e?.CustomCrestAssetId ?? h.CustomCrestAssetId,
                Record = e is null ? null : ToRecord(e),
                Source = e is null ? "history" : "both",
                TimesFaced = h.TimesFaced,
                LastFacedAt = h.LastFacedAt
            });
        }

        foreach (var e in ea.Items)
        {
            if (clubId.HasValue && e.ClubId == clubId.Value) continue;
            if (!used.Add(e.ClubId)) continue;

            // Clube que já enfrentamos mas cujo nome não casou com o texto (ex.: renomeado): ainda mostra o histórico
            var known = history.FirstOrDefault(h => h.ClubId == e.ClubId);
            results.Add(new OpponentSearchItemDto
            {
                ClubId = e.ClubId,
                Name = e.Name,
                CurrentDivision = e.CurrentDivision,
                Division = e.CurrentDivision ?? known?.Division,
                SkillRating = e.SkillRating,
                TeamId = TeamIdOf(e.CrestAssetId ?? known?.CrestAssetId),
                ReputationTier = e.ReputationTier,
                CrestAssetId = e.CrestAssetId ?? known?.CrestAssetId,
                CustomCrestAssetId = e.CustomCrestAssetId ?? known?.CustomCrestAssetId,
                Record = ToRecord(e),
                Source = known is null ? "ea" : "both",
                TimesFaced = known?.TimesFaced ?? 0,
                LastFacedAt = known?.LastFacedAt
            });
        }

        return new OpponentSearchResponseDto
        {
            Query = q,
            EaAvailable = ea.Available,
            EaTruncated = ea.Available && ea.Truncated,
            Results = results.Take(take).ToList()
        };
    }

    private static long? TeamIdOf(string? crestAssetId) =>
        long.TryParse(crestAssetId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) && id > 0 ? id : null;

    private static OpponentSearchRecordDto? ToRecord(EaClubInfo e)
    {
        if (e.Wins is null && e.Ties is null && e.Losses is null && e.GamesPlayed is null) return null;
        var wins = e.Wins ?? 0;
        var draws = e.Ties ?? 0;
        var losses = e.Losses ?? 0;
        var games = e.GamesPlayed ?? (wins + draws + losses);
        return new OpponentSearchRecordDto { Games = games, Wins = wins, Draws = draws, Losses = losses };
    }

    /// <summary>0 = começa com; 1 = contém; 2 = todos os termos contidos; -1 = não casa.</summary>
    internal static int Rank(string normalizedName, string normalizedQuery, string[] tokens)
    {
        if (normalizedName.StartsWith(normalizedQuery, StringComparison.Ordinal)) return 0;
        if (normalizedName.Contains(normalizedQuery, StringComparison.Ordinal)) return 1;
        if (tokens.Length > 1 && tokens.All(t => normalizedName.Contains(t, StringComparison.Ordinal))) return 2;
        return -1;
    }

    /// <summary>Minúsculas, sem acentos, pontuação vira espaço e espaços repetidos são colapsados.</summary>
    internal static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        var decomposed = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        var lastWasSpace = true;
        foreach (var ch in decomposed)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (cat == UnicodeCategory.NonSpacingMark) continue;

            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(char.ToLowerInvariant(ch));
                lastWasSpace = false;
            }
            else if (!lastWasSpace)
            {
                sb.Append(' ');
                lastWasSpace = true;
            }
        }
        return sb.ToString().Trim();
    }

    private async Task<List<HistoryOpponent>> GetHistoryAsync(long clubId, CancellationToken ct)
    {
        var key = $"goalreg:history:{clubId}";
        if (_cache.TryGetValue(key, out List<HistoryOpponent>? cached) && cached is not null)
            return cached;

        var rows = await _db.MatchClubs.AsNoTracking()
            .Where(mc => mc.ClubId != clubId && mc.Match.Clubs.Any(c => c.ClubId == clubId))
            .Select(mc => new
            {
                mc.ClubId,
                Name = mc.Details != null ? mc.Details.Name : null,
                TeamId = mc.Details != null ? (long?)mc.Details.TeamId : null,
                CustomCrest = mc.Details != null ? mc.Details.CrestAssetId : null,
                mc.CurrentDivision,
                mc.Match.Timestamp
            })
            .ToListAsync(ct);

        var list = rows
            .GroupBy(r => r.ClubId)
            .Select(g =>
            {
                var ordered = g.OrderByDescending(r => r.Timestamp).ToList();
                var name = ordered.Select(r => r.Name).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)) ?? $"Clube {g.Key}";
                // Escudo = teamId do clube (mesmo identificador usado pelo resto do site)
                var teamId = ordered.Select(r => r.TeamId).FirstOrDefault(t => t is > 0);
                var crest = teamId is > 0 ? teamId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : null;
                var division = ordered.Select(r => r.CurrentDivision).FirstOrDefault(d => d.HasValue);
                var customCrest = ordered.Select(r => r.CustomCrest).FirstOrDefault(c => !string.IsNullOrWhiteSpace(c));
                return new HistoryOpponent(g.Key, name, Normalize(name), crest, division, ordered.Count, ordered[0].Timestamp, customCrest);
            })
            .ToList();

        _cache.Set(key, list, HistoryTtl);
        return list;
    }
}
