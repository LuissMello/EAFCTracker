// Infrastructure/Http/BackendCaller.cs
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace EAFCMatchTracker.Infrastructure.Http
{
    /// <summary>
    /// Cliente reutilizável para chamadas ao backend EA, já configurando headers esperados.
    /// </summary>
    public sealed class EAHttpClient : IEAHttpClient
    {
        private readonly HttpClient _http;

        public EAHttpClient(HttpClient http)
        {
            _http = http;
            // Timeout pode ser ajustado por DI, mas um default sensato ajuda:
            if (_http.Timeout == default) _http.Timeout = TimeSpan.FromSeconds(60);
        }

        public async Task<string?> GetStringAsync(Uri endpoint, CancellationToken ct)
        {
            using var resp = await SendAsync(HttpMethod.Get, endpoint, content: null, ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadAsStringAsync(ct);
        }

        public async Task<HttpResponseMessage> SendAsync(HttpMethod method, Uri endpoint, HttpContent? content, CancellationToken ct)
        {
            using var req = new HttpRequestMessage(method, endpoint)
            {
                Version = HttpVersion.Version20,
                VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher,
                Content = content
            };

            // Headers “de navegador”
            req.Headers.UserAgent.ParseAdd("PostmanRuntime/7.46.1");
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            req.Headers.AcceptEncoding.ParseAdd("gzip");
            req.Headers.AcceptEncoding.ParseAdd("deflate");
            req.Headers.AcceptEncoding.ParseAdd("br");
            req.Headers.Connection.Clear();
            req.Headers.Connection.Add("keep-alive");

            // Host correto (com porta se aplicável)
            var hostHeader = endpoint.IsDefaultPort ? endpoint.IdnHost : $"{endpoint.IdnHost}:{endpoint.Port}";
            req.Headers.Host = hostHeader;

            // ResponseHeadersRead evita buffer desnecessário em payloads grandes
            return await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct)
                              .ConfigureAwait(false);
        }
    }
}
