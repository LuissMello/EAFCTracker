using System.Globalization;
using System.Text.Json;
using EAFCMatchTracker.Application.Dtos;
using EAFCMatchTracker.Application.Exceptions;
using EAFCMatchTracker.Application.Interfaces.Services;
using EAFCMatchTracker.Infrastructure.Data;
using EAFCMatchTracker.Infrastructure.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EAFCMatchTracker.Application.Services;

public sealed class OpponentPreviewService : IOpponentPreviewService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(10);
    private const int MaxName = 100;

    private const string DefaultOverallEndpoint = "/clubs/overallStats?platform=common-gen5&clubIds={0}";
    private const string DefaultMatchesEndpoint = "/clubs/matches?matchType={1}&platform=common-gen5&clubIds={0}&maxResultCount=20";
    private const string DefaultMembersEndpoint = "/members/stats?platform=common-gen5&clubId={0}";

    private readonly EAFCContext _db;
    private readonly IEAHttpClient _ea;
    private readonly IEaClubSearchClient _search;
    private readonly IConfiguration _config;
    private readonly IMemoryCache _cache;
    private readonly ILogger<OpponentPreviewService> _logger;

    public OpponentPreviewService(
        EAFCContext db, IEAHttpClient ea, IEaClubSearchClient search, IConfiguration config,
        IMemoryCache cache, ILogger<OpponentPreviewService> logger)
    {
        _db = db;
        _ea = ea;
        _search = search;
        _config = config;
        _cache = cache;
        _logger = logger;
    }

    // Parte do preview que vem da EA (cacheada). Campos nulos = bloco indisponível.
    private sealed record EaPart(
        EaClubInfo? Search, bool SearchAvailable, EaOverall? Overall,
        OpponentPreviewRecentDto? Recent, OpponentPreviewMembersDto? Members, List<string> Warnings);

    private sealed record EaOverall(
        int? Games, int? Wins, int? Ties, int? Losses, int? Goals, int? GoalsAgainst,
        int? Promotions, int? Relegations, int? BestDivision, int? ReputationTier);

    public async Task<OpponentPreviewDto> GetPreviewAsync(long opponentClubId, long? clubId, string? name, CancellationToken ct)
    {
        if (opponentClubId <= 0) throw new DomainValidationException("Clube adversário inválido.");

        var cleanName = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        if (cleanName is { Length: > MaxName })
            throw new DomainValidationException($"O nome do adversário deve ter no máximo {MaxName} caracteres.");

        // Dados locais (nunca falham por causa da EA)
        var local = await _db.MatchClubs.AsNoTracking()
            .Where(mc => mc.ClubId == opponentClubId)
            .OrderByDescending(mc => mc.Match.Timestamp)
            .Select(mc => new
            {
                Name = mc.Details != null ? mc.Details.Name : null,
                Crest = mc.Details != null ? mc.Details.CrestAssetId : null,
                mc.CurrentDivision
            })
            .Take(20)
            .ToListAsync(ct);
        var dbName = local.Select(l => l.Name).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));
        var dbCrest = local.Select(l => l.Crest).FirstOrDefault(c => !string.IsNullOrWhiteSpace(c));
        var dbDivision = local.Select(l => l.CurrentDivision).FirstOrDefault(d => d.HasValue);

        var effectiveName = cleanName ?? dbName;
        var eaPart = await GetEaPartAsync(opponentClubId, effectiveName, ct);

        var dto = new OpponentPreviewDto { ClubId = opponentClubId, Name = effectiveName };
        var warnings = new List<string>(eaPart.Warnings);

        var s = eaPart.Search;
        var o = eaPart.Overall;

        dto.Name = s?.Name ?? effectiveName;
        dto.CrestAssetId = s?.CrestAssetId ?? dbCrest;
        dto.CurrentDivision = s?.CurrentDivision ?? dbDivision;
        dto.BestDivision = s?.BestDivision ?? o?.BestDivision;
        dto.ReputationTier = s?.ReputationTier ?? o?.ReputationTier;
        dto.Points = s?.Points;
        dto.Promotions = s?.Promotions ?? o?.Promotions;
        dto.Relegations = s?.Relegations ?? o?.Relegations;

        // Retrospecto/gols: preferem os dados da busca (já em cache), com overallStats como fallback
        var wins = s?.Wins ?? o?.Wins;
        var ties = s?.Ties ?? o?.Ties;
        var losses = s?.Losses ?? o?.Losses;
        if (wins.HasValue || ties.HasValue || losses.HasValue)
        {
            var w = wins ?? 0; var d = ties ?? 0; var l = losses ?? 0;
            var games = w + d + l;
            if (games == 0) games = s?.GamesPlayed ?? o?.Games ?? 0;
            dto.Record = new OpponentPreviewRecordDto
            {
                Games = games, Wins = w, Draws = d, Losses = l,
                WinRatePct = games > 0 ? Math.Round(w * 100.0 / games, 1) : 0
            };

            var gf = s?.Goals ?? o?.Goals;
            var ga = s?.GoalsAgainst ?? o?.GoalsAgainst;
            if (gf.HasValue && ga.HasValue)
            {
                dto.Goals = new OpponentPreviewGoalsDto
                {
                    For = gf.Value, Against = ga.Value,
                    AvgFor = games > 0 ? Math.Round(gf.Value / (double)games, 2) : 0,
                    AvgAgainst = games > 0 ? Math.Round(ga.Value / (double)games, 2) : 0
                };
            }
        }

        dto.Recent = eaPart.Recent;
        dto.Members = eaPart.Members;
        dto.HeadToHead = clubId.HasValue && clubId.Value > 0 && clubId.Value != opponentClubId
            ? await GetHeadToHeadAsync(clubId.Value, opponentClubId, ct)
            : null;

        if (dto.CurrentDivision is null && eaPart.SearchAvailable && s is null && effectiveName is not null)
            warnings.Add("Divisão e nível de reputação não encontrados na busca da EA para este nome.");

        dto.Warnings = warnings.Distinct().ToList();
        return dto;
    }

    // ───────────────────────── EA ─────────────────────────

    private async Task<EaPart> GetEaPartAsync(long opponentId, string? name, CancellationToken ct)
    {
        var key = $"goalreg:preview:{opponentId}:{(name ?? string.Empty).ToLowerInvariant()}";
        if (_cache.TryGetValue(key, out EaPart? cached) && cached is not null)
            return cached;

        var warnings = new List<string>();

        // 1) busca por nome (já traz overall embutido) + 2) últimas partidas + 3) membros, em paralelo
        var searchTask = SafeAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(name)) return (EaSearchResult?)null;
            return await _search.SearchAsync(name!, ct);
        }, ct);
        var recentTask = SafeAsync(() => FetchRecentAsync(opponentId, ct), ct);
        var membersTask = SafeAsync(() => FetchMembersAsync(opponentId, ct), ct);
        await Task.WhenAll(searchTask, recentTask, membersTask);

        var searchResult = await searchTask;
        EaClubInfo? info = null;
        var searchAvailable = false;
        if (string.IsNullOrWhiteSpace(name))
        {
            warnings.Add("Nome do adversário não informado: divisão e reputação não foram consultadas.");
        }
        else if (searchResult is null || !searchResult.Available)
        {
            warnings.Add("Não foi possível consultar a busca da EA (divisão, reputação e retrospecto).");
        }
        else
        {
            searchAvailable = true;
            info = searchResult.Items.FirstOrDefault(i => i.ClubId == opponentId);
        }

        // overallStats: complemento/fallback quando a busca não traz o clube
        EaOverall? overall = null;
        var needOverall = info is null || info.Wins is null || info.Goals is null || info.BestDivision is null;
        if (needOverall)
        {
            overall = await SafeAsync(() => FetchOverallAsync(opponentId, ct), ct);
            if (overall is null && info is null)
                warnings.Add("Não foi possível obter as estatísticas gerais do clube na EA.");
        }

        var recent = await recentTask;
        if (recent is null)
            warnings.Add("Não foi possível obter as últimas partidas do adversário na EA.");
        else if (recent.MatchesAnalyzed == 0)
        {
            warnings.Add("A EA não retornou partidas recentes do adversário.");
            recent = null;
        }

        var members = await membersTask;
        if (members is null)
            warnings.Add("Não foi possível obter o elenco do adversário na EA.");

        var part = new EaPart(info, searchAvailable, overall, recent, members, warnings);

        // Só guarda em cache se algo veio da EA (falhas totais são tentadas de novo na próxima chamada)
        if (info is not null || overall is not null || recent is not null || members is not null)
            _cache.Set(key, part, CacheTtl);

        return part;
    }

    /// <summary>Cada chamada à EA é independente: falha (exceto cancelamento do cliente) vira null.</summary>
    private async Task<T?> SafeAsync<T>(Func<Task<T?>> call, CancellationToken ct) where T : class
    {
        try
        {
            return await call();
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Falha em chamada à EA no preview do adversário.");
            return null;
        }
    }

    private Uri BuildUri(string template, params object[] args)
    {
        var baseUrl = _config["EAFCSettings:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("EAFCSettings:BaseUrl não configurado.");
        var relative = string.Format(CultureInfo.InvariantCulture, template, args);
        return new Uri(baseUrl.TrimEnd('/') + "/" + relative.TrimStart('/'));
    }

    private async Task<string?> GetJsonAsync(Uri uri, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(CallTimeout);
        return await _ea.GetStringAsync(uri, cts.Token);
    }

    private async Task<EaOverall?> FetchOverallAsync(long opponentId, CancellationToken ct)
    {
        var tpl = _config["EAFCSettings:OverallStatsEndpoint"] ?? DefaultOverallEndpoint;
        var json = await GetJsonAsync(BuildUri(tpl, opponentId), ct);
        if (string.IsNullOrWhiteSpace(json)) return null;

        using var doc = JsonDocument.Parse(json);
        var el = doc.RootElement;
        if (el.ValueKind == JsonValueKind.Array)
        {
            if (el.GetArrayLength() == 0) return null;
            el = el[0];
        }
        if (el.ValueKind != JsonValueKind.Object) return null;

        return new EaOverall(
            el.Int("gamesPlayed"), el.Int("wins"), el.Int("ties"), el.Int("losses"),
            el.Int("goals"), el.Int("goalsAgainst"), el.Int("promotions"), el.Int("relegations"),
            el.Int("bestDivision"), el.Int("reputationtier"));
    }

    private async Task<OpponentPreviewMembersDto?> FetchMembersAsync(long opponentId, CancellationToken ct)
    {
        var tpl = _config["EAFCSettings:MembersStatsEndpoint"] ?? DefaultMembersEndpoint;
        var json = await GetJsonAsync(BuildUri(tpl, opponentId), ct);
        if (string.IsNullOrWhiteSpace(json)) return null;

        using var doc = JsonDocument.Parse(json);
        var members = doc.RootElement.Prop("members");
        if (members is null || members.Value.ValueKind != JsonValueKind.Array) return null;

        var overalls = new List<int>();
        var count = 0;
        foreach (var m in members.Value.EnumerateArray())
        {
            count++;
            var ov = m.Int("proOverall");
            if (ov is > 0) overalls.Add(ov.Value);
        }

        return new OpponentPreviewMembersDto
        {
            Count = count,
            AvgOverall = overalls.Count > 0 ? Math.Round(overalls.Average(), 1) : null
        };
    }

    private async Task<OpponentPreviewRecentDto?> FetchRecentAsync(long opponentId, CancellationToken ct)
    {
        var tpl = _config["EAFCSettings:ClubMatchesEndpoint"] ?? DefaultMatchesEndpoint;

        var items = await FetchMatchesAsync(tpl, opponentId, "leagueMatch", ct);
        if (items is { Count: 0 })
            items = await FetchMatchesAsync(tpl, opponentId, "playoffMatch", ct);
        if (items is null) return null;

        var last = items.OrderByDescending(i => i.Timestamp).Take(5).ToList();
        if (last.Count == 0) return new OpponentPreviewRecentDto { MatchesAnalyzed = 0 };

        var withPlayers = last.Where(i => i.Players.HasValue).Select(i => i.Players!.Value).ToList();
        return new OpponentPreviewRecentDto
        {
            MatchesAnalyzed = last.Count,
            Results = last.Select(i => i.Result).ToList(),
            AvgGoalsFor = Math.Round(last.Average(i => i.GoalsFor), 2),
            AvgGoalsAgainst = Math.Round(last.Average(i => i.GoalsAgainst), 2),
            LastMatchPlayers = last[0].Players,
            AvgPlayersLast5 = withPlayers.Count > 0 ? Math.Round(withPlayers.Average(), 1) : null,
            LastPlayedAt = last[0].Timestamp
        };
    }

    private sealed record RecentMatch(DateTime Timestamp, string Result, int GoalsFor, int GoalsAgainst, int? Players);

    private async Task<List<RecentMatch>?> FetchMatchesAsync(string template, long opponentId, string matchType, CancellationToken ct)
    {
        var json = await GetJsonAsync(BuildUri(template, opponentId, matchType), ct);
        if (string.IsNullOrWhiteSpace(json)) return null;

        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array) return null;

        var key = opponentId.ToString(CultureInfo.InvariantCulture);
        var list = new List<RecentMatch>();
        foreach (var m in doc.RootElement.EnumerateArray())
        {
            var club = m.Prop("clubs")?.Prop(key);
            if (club is null || club.Value.ValueKind != JsonValueKind.Object) continue;

            var gf = club.Value.Int("goals");
            var ga = club.Value.Int("goalsAgainst");
            if (gf is null || ga is null) continue;

            var ts = m.Long("timestamp");
            var when = ts.HasValue ? DateTimeOffset.FromUnixTimeSeconds(ts.Value).UtcDateTime : DateTime.MinValue;

            int? players = null;
            var p = m.Prop("players")?.Prop(key);
            if (p is { ValueKind: JsonValueKind.Object }) players = p.Value.EnumerateObject().Count();

            var result = gf > ga ? "W" : gf < ga ? "L" : "D";
            list.Add(new RecentMatch(when, result, gf.Value, ga.Value, players));
        }

        return list;
    }

    // ───────────────────────── confronto direto (nosso banco) ─────────────────────────

    private async Task<OpponentPreviewHeadToHeadDto?> GetHeadToHeadAsync(long clubId, long opponentId, CancellationToken ct)
    {
        var rows = await _db.MatchClubs.AsNoTracking()
            .Where(mc => mc.ClubId == clubId && mc.Match.Clubs.Any(c => c.ClubId == opponentId))
            .Select(mc => new
            {
                mc.Goals,
                Against = mc.Match.Clubs.Where(c => c.ClubId == opponentId).Select(c => (int)c.Goals).FirstOrDefault(),
                mc.Match.Timestamp
            })
            .ToListAsync(ct);

        if (rows.Count == 0) return null;

        return new OpponentPreviewHeadToHeadDto
        {
            Games = rows.Count,
            Wins = rows.Count(r => r.Goals > r.Against),
            Draws = rows.Count(r => r.Goals == r.Against),
            Losses = rows.Count(r => r.Goals < r.Against),
            GoalsFor = rows.Sum(r => (int)r.Goals),
            GoalsAgainst = rows.Sum(r => r.Against),
            LastPlayedAt = rows.Max(r => r.Timestamp)
        };
    }
}
