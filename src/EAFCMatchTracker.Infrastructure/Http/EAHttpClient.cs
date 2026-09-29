// Infrastructure/Http/BackendCaller.cs
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace EAFCMatchTracker.Infrastructure.Http
{
    /// <summary>
    /// Cliente reutilizável para chamadas ao backend EA, já configurando headers esperados.
    /// </summary>
    public sealed class EAHttpClient : IEAHttpClient
    {
        private const int MaxRetries = 3;
        private static readonly TimeSpan BaseDelay = TimeSpan.FromMilliseconds(500);
        private static readonly TimeSpan MaxRetryAfter = TimeSpan.FromSeconds(30);

        private readonly HttpClient _http;
        private readonly ILogger<EAHttpClient>? _logger;

        public EAHttpClient(HttpClient http, ILogger<EAHttpClient>? logger = null)
        {
            _http = http;
            _logger = logger;
            // Timeout pode ser ajustado por DI, mas um default sensato ajuda:
            if (_http.Timeout == default) _http.Timeout = TimeSpan.FromSeconds(60);
        }

        public async Task<string?> GetStringAsync(Uri endpoint, CancellationToken ct)
        {
            using var resp = await SendAsync(HttpMethod.Get, endpoint, content: null, ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger?.LogWarning(
                    "EA API retornou {StatusCode} para {Endpoint}; retornando null.",
                    (int)resp.StatusCode, endpoint.GetLeftPart(UriPartial.Path));
                return null;
            }
            return await resp.Content.ReadAsStringAsync(ct);
        }

        public async Task<HttpResponseMessage> SendAsync(HttpMethod method, Uri endpoint, HttpContent? content, CancellationToken ct)
        {
            for (var attempt = 0; ; attempt++)
            {
                HttpResponseMessage resp;
                try
                {
                    resp = await SendOnceAsync(method, endpoint, content, ct).ConfigureAwait(false);
                }
                catch (HttpRequestException ex) when (attempt < MaxRetries)
                {
                    var delay = ComputeDelay(attempt, null);
                    _logger?.LogWarning(ex,
                        "Falha de rede ao chamar EA ({Endpoint}); tentativa {Attempt}/{Max}, aguardando {Delay}ms.",
                        endpoint.GetLeftPart(UriPartial.Path), attempt + 1, MaxRetries, (int)delay.TotalMilliseconds);
                    await Task.Delay(delay, ct).ConfigureAwait(false);
                    continue;
                }

                if (!IsTransient(resp.StatusCode) || attempt >= MaxRetries)
                    return resp;

                var wait = ComputeDelay(attempt, resp);
                _logger?.LogWarning(
                    "EA API retornou {StatusCode} para {Endpoint}; tentativa {Attempt}/{Max}, aguardando {Delay}ms.",
                    (int)resp.StatusCode, endpoint.GetLeftPart(UriPartial.Path), attempt + 1, MaxRetries, (int)wait.TotalMilliseconds);
                resp.Dispose();
                await Task.Delay(wait, ct).ConfigureAwait(false);
            }
        }

        private static bool IsTransient(HttpStatusCode code) =>
            code is HttpStatusCode.TooManyRequests
                 or HttpStatusCode.BadGateway
                 or HttpStatusCode.ServiceUnavailable
                 or HttpStatusCode.GatewayTimeout;

        private static TimeSpan ComputeDelay(int attempt, HttpResponseMessage? resp)
        {
            var retryAfter = resp?.Headers.RetryAfter;
            if (retryAfter is not null)
            {
                TimeSpan? requested = retryAfter.Delta
                    ?? (retryAfter.Date.HasValue ? retryAfter.Date.Value - DateTimeOffset.UtcNow : null);
                if (requested.HasValue)
                {
                    if (requested.Value < TimeSpan.Zero) requested = TimeSpan.Zero;
                    return requested.Value > MaxRetryAfter ? MaxRetryAfter : requested.Value;
                }
            }

            var exp = TimeSpan.FromMilliseconds(BaseDelay.TotalMilliseconds * Math.Pow(2, attempt));
            var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 250));
            var total = exp + jitter;
            return total > MaxRetryAfter ? MaxRetryAfter : total;
        }

        private async Task<HttpResponseMessage> SendOnceAsync(HttpMethod method, Uri endpoint, HttpContent? content, CancellationToken ct)
        {
            using var req = new HttpRequestMessage(method, endpoint)
            {
                Version = HttpVersion.Version20,
                VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher,
                Content = content
            };

            // User-Agent de Chrome real (atualizar periodicamente com versão atual)
            req.Headers.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
                "AppleWebKit/537.36 (KHTML, like Gecko) " +
                "Chrome/131.0.0.0 Safari/537.36");

            req.Headers.Accept.ParseAdd("application/json, text/plain, */*");
            req.Headers.AcceptEncoding.ParseAdd("gzip, deflate, br, zstd");
            req.Headers.AcceptLanguage.ParseAdd("pt-BR,pt;q=0.9,en-US;q=0.8,en;q=0.7");

            // Chrome client hints — ausência desses headers é sinal de bot
            req.Headers.TryAddWithoutValidation("sec-ch-ua",
                "\"Google Chrome\";v=\"131\", \"Chromium\";v=\"131\", \"Not_A Brand\";v=\"24\"");
            req.Headers.TryAddWithoutValidation("sec-ch-ua-mobile", "?0");
            req.Headers.TryAddWithoutValidation("sec-ch-ua-platform", "\"Windows\"");

            // Sec-Fetch headers — browser sempre envia, bots geralmente não
            req.Headers.TryAddWithoutValidation("Sec-Fetch-Dest", "empty");
            req.Headers.TryAddWithoutValidation("Sec-Fetch-Mode", "cors");
            req.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "same-site");

            req.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };

            // Host correto (com porta se aplicável)
            var hostHeader = endpoint.IsDefaultPort ? endpoint.IdnHost : $"{endpoint.IdnHost}:{endpoint.Port}";
            req.Headers.Host = hostHeader;

            // ResponseHeadersRead evita buffer desnecessário em payloads grandes
            return await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct)
                              .ConfigureAwait(false);
        }
    }
}
