using System.Text.Json;
using EAFCMatchTracker.Application.Interfaces.Services;
using EAFCMatchTracker.Infrastructure.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EAFCMatchTracker.Application.Services;

public sealed class EaClubSearchClient : IEaClubSearchClient
{
    private const string DefaultSearchEndpoint = "/allTimeLeaderboard/search?platform=common-gen5&clubName={0}";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(5);
    // Depois de uma falha, não insiste por um instante: sem isto cada tecla digitada esperaria o timeout/retries (~8s) de novo.
    private static readonly TimeSpan DownTtl = TimeSpan.FromSeconds(30);
    private const string DownKey = "ea:clubsearch:down";

    private readonly IEAHttpClient _ea;
    private readonly IConfiguration _config;
    private readonly IMemoryCache _cache;
    private readonly ILogger<EaClubSearchClient> _logger;

    public EaClubSearchClient(IEAHttpClient ea, IConfiguration config, IMemoryCache cache, ILogger<EaClubSearchClient> logger)
    {
        _ea = ea;
        _config = config;
        _cache = cache;
        _logger = logger;
    }

    public async Task<EaSearchResult> SearchAsync(string name, CancellationToken ct)
    {
        var term = (name ?? string.Empty).Trim();
        if (term.Length == 0) return new EaSearchResult(true, Array.Empty<EaClubInfo>());

        var key = "ea:clubsearch:" + term.ToLowerInvariant();
        if (_cache.TryGetValue(key, out EaSearchResult? cached) && cached is not null)
            return cached;

        if (_cache.TryGetValue(DownKey, out _)) return EaSearchResult.Unavailable;

        var baseUrl = _config["EAFCSettings:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            _logger.LogWarning("EAFCSettings:BaseUrl não configurado; busca de adversários na EA indisponível.");
            return EaSearchResult.Unavailable;
        }

        var template = _config["EAFCSettings:SearchClubsEndpoint"] ?? DefaultSearchEndpoint;
        var uri = new Uri(baseUrl.TrimEnd('/') + string.Format(template, Uri.EscapeDataString(term)));

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(CallTimeout);

            var json = await _ea.GetStringAsync(uri, cts.Token);
            if (json is null)
            {
                _cache.Set(DownKey, true, DownTtl);
                return EaSearchResult.Unavailable;
            }

            var result = new EaSearchResult(true, Parse(json));
            _cache.Set(key, result, CacheTtl);
            return result;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Falha ao consultar a busca de clubes da EA.");
            _cache.Set(DownKey, true, DownTtl);
            return EaSearchResult.Unavailable;
        }
    }

    internal static IReadOnlyList<EaClubInfo> Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Array) return Array.Empty<EaClubInfo>();

        var list = new List<EaClubInfo>();
        var seen = new HashSet<long>();
        foreach (var item in root.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;

            var info = item.Prop("clubInfo");
            var id = item.Long("clubId") ?? info?.Long("clubId") ?? 0;
            if (id <= 0 || !seen.Add(id)) continue;

            var name = info?.Str("name") ?? item.Str("clubName") ?? $"Clube {id}";
            // O escudo no CDN da EA é identificado pelo teamId do clube (é o que o resto do site usa em crestUrl);
            // o crestAssetId do customKit é outro tipo de arquivo e não resolve para a imagem do escudo.
            var teamId = info?.Long("teamId");
            var crest = teamId is > 0 ? teamId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : null;
            // Alternativa para clubes cujo teamId não tem imagem no CDN (o frontend tenta esta antes do logo genérico)
            var customCrest = info?.Prop("customKit")?.Str("crestAssetId");

            list.Add(new EaClubInfo(
                id, name,
                item.Int("currentDivision"), item.Int("bestDivision"), item.Int("reputationtier"), item.Int("points"),
                item.Int("gamesPlayed"), item.Int("wins"), item.Int("ties"), item.Int("losses"),
                item.Int("goals"), item.Int("goalsAgainst"), item.Int("promotions"), item.Int("relegations"),
                string.IsNullOrWhiteSpace(crest) ? null : crest,
                string.IsNullOrWhiteSpace(customCrest) ? null : customCrest));
        }

        return list;
    }
}
