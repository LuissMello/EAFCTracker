// Infrastructure/Http/IBackendCaller.cs
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System;

namespace EAFCMatchTracker.Infrastructure.Http
{
    public interface IEAHttpClient
    {
        Task<string?> GetStringAsync(Uri endpoint, CancellationToken ct);
        Task<HttpResponseMessage> SendAsync(HttpMethod method, Uri endpoint, HttpContent? content, CancellationToken ct);
    }
}
