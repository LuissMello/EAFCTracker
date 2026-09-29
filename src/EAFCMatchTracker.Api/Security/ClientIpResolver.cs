using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Primitives;

namespace EAFCMatchTracker.Api.Security;

/// <summary>
/// Identifica o cliente para o controle de tentativas de login. Os headers de proxy só são considerados quando a
/// aplicação roda atrás do proxy do Fly.io (variável FLY_APP_NAME definida); fora dele, qualquer cliente poderia
/// forjá-los para escapar do bloqueio. IPv6 é agrupado pelo prefixo /64.
/// </summary>
public static class ClientIpResolver
{
    public const string FlyClientIpHeader = "Fly-Client-IP";
    public const string ForwardedForHeader = "X-Forwarded-For";

    public static string Resolve(HttpContext context, bool behindFlyProxy)
    {
        if (behindFlyProxy)
        {
            var fly = FirstValue(context.Request.Headers[FlyClientIpHeader]);
            if (TryNormalize(fly, out var flyIp))
                return flyIp;

            var forwarded = FirstValue(context.Request.Headers[ForwardedForHeader]);
            var first = forwarded?.Split(',', 2)[0];
            if (TryNormalize(first, out var xffIp))
                return xffIp;
        }

        var remote = context.Connection.RemoteIpAddress;
        return remote is null ? "unknown" : Normalize(remote);
    }

    private static string? FirstValue(StringValues values) => values.Count > 0 ? values[0] : null;

    private static bool TryNormalize(string? raw, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(raw) || raw.Length > 64)
            return false;
        if (!IPAddress.TryParse(raw.Trim(), out var ip))
            return false;

        normalized = Normalize(ip);
        return true;
    }

    private static string Normalize(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6)
            ip = ip.MapToIPv4();

        if (ip.AddressFamily != AddressFamily.InterNetworkV6)
            return ip.ToString();

        var bytes = ip.GetAddressBytes();
        Array.Clear(bytes, 8, 8);
        return new IPAddress(bytes) + "/64";
    }
}
