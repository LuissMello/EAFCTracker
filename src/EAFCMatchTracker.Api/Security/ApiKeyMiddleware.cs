using Microsoft.AspNetCore.Mvc;

namespace EAFCMatchTracker.Api.Security;

/// <summary>
/// Protege as ações administrativas. Exige UMA das credenciais:
///  - <c>Authorization: Bearer &lt;token&gt;</c> (token de sessão emitido por POST /api/auth/login); ou
///  - <c>X-Api-Key</c> == Auth:ApiKey (mantido para scripts/curl).
/// Rotas protegidas:
///  - todo método diferente de GET/HEAD/OPTIONS sob /api/*, exceto POST /api/fetch/run, POST /api/fetch/live,
///    POST /api/auth/login, POST/PUT/DELETE /api/goal-registrations/** (registro de gols, com rate limit por IP) e
///    POST /api/matches/{id}/goals (vínculo manual de gols, com rate limit por IP);
///  - TODOS os métodos (inclusive GET) sob /api/admin/* e /api/maintenance/*.
/// OPTIONS (preflight) sempre passa. O bypass vale SOMENTE em Development com Auth:ApiKey vazia/não configurada;
/// se a chave estiver configurada, a checagem é aplicada inclusive em Development. Fora de Development sem chave
/// configurada, rotas protegidas retornam 401.
/// Respostas 401 (ProblemDetails) trazem a extensão <c>code</c>: "unauthorized" (sem credenciais/inválidas) ou
/// "token_expired" (Bearer válido porém expirado -> o frontend deve pedir login novamente).
/// </summary>
public sealed class ApiKeyMiddleware
{
    public const string HeaderName = "X-Api-Key";

    /// <summary>Chave em HttpContext.Items com o instante de expiração (DateTimeOffset) quando autenticado por token.</summary>
    public const string TokenExpiresItemKey = "Admin:TokenExpiresAtUtc";

    public const string CodeUnauthorized = "unauthorized";
    public const string CodeTokenExpired = "token_expired";

    private const string BearerPrefix = "Bearer ";

    private static readonly PathString ApiRoot = new("/api");
    private static readonly PathString AdminRoot = new("/api/admin");
    private static readonly PathString MaintenanceRoot = new("/api/maintenance");

    private static readonly PathString GoalRegistrationsRoot = new("/api/goal-registrations");
    private static readonly PathString MatchesRoot = new("/api/matches");

    private static readonly string[] PublicPostPaths = ["/api/fetch/run", "/api/fetch/live", "/api/auth/login"];

    private readonly RequestDelegate _next;
    private readonly IHostEnvironment _env;
    private readonly IAdminTokenService _tokens;

    public ApiKeyMiddleware(RequestDelegate next, IHostEnvironment env, IAdminTokenService tokens)
    {
        _next = next;
        _env = env;
        _tokens = tokens;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!RequiresAuth(context.Request) || (_env.IsDevelopment() && !_tokens.IsConfigured))
        {
            await _next(context);
            return;
        }

        if (HasValidApiKey(context.Request))
        {
            await _next(context);
            return;
        }

        var bearer = GetBearerToken(context.Request);
        if (bearer is not null)
        {
            var validation = _tokens.Validate(bearer);
            if (validation.Status == AdminTokenStatus.Valid)
            {
                context.Items[TokenExpiresItemKey] = validation.ExpiresAtUtc!.Value;
                await _next(context);
                return;
            }

            if (validation.Status == AdminTokenStatus.Expired)
            {
                await RejectAsync(context, CodeTokenExpired, "Sessão expirada. Faça login novamente.",
                    "Bearer error=\"invalid_token\", error_description=\"The token expired\"");
            }
            else
            {
                await RejectAsync(context, CodeUnauthorized, "Token inválido. Faça login novamente.",
                    "Bearer error=\"invalid_token\"");
            }

            return;
        }

        await RejectAsync(context, CodeUnauthorized, "Autenticação necessária.", "Bearer", "ApiKey");
    }

    private bool HasValidApiKey(HttpRequest request) =>
        request.Headers.TryGetValue(HeaderName, out var provided)
        && provided.Count == 1
        && _tokens.VerifyPassword(provided[0]);

    // Extrai o token de "Authorization: Bearer <token>" (esquema case-insensitive); null se ausente/outro esquema.
    private static string? GetBearerToken(HttpRequest request)
    {
        if (!request.Headers.TryGetValue("Authorization", out var values) || values.Count != 1)
            return null;

        var header = values[0];
        if (header is null || !header.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
            return null;

        return header[BearerPrefix.Length..].Trim();
    }

    private static async Task RejectAsync(HttpContext context, string code, string detail, params string[] challenges)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers.WWWAuthenticate = challenges;

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Unauthorized",
            Detail = detail,
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.2"
        };
        problem.Extensions["code"] = code;

        await context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json");
    }

    private static bool RequiresAuth(HttpRequest request)
    {
        var path = request.Path;
        if (!path.StartsWithSegments(ApiRoot))
            return false;

        var method = request.Method;
        if (HttpMethods.IsOptions(method))
            return false;

        if (path.StartsWithSegments(AdminRoot) || path.StartsWithSegments(MaintenanceRoot))
            return true;

        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method))
            return false;

        if (IsPublicGoalWrite(path, method))
            return false;

        if (HttpMethods.IsPost(method))
        {
            var normalized = (path.Value ?? string.Empty).TrimEnd('/');
            foreach (var p in PublicPostPaths)
            {
                if (string.Equals(normalized, p, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
        }

        return true;
    }

    // Escritas públicas do registro de gols (rate limit por IP aplicado nos controllers):
    //  - POST/PUT/DELETE /api/goal-registrations e subrotas (admin: /api/admin/goal-registrations/* continua protegido);
    //  - POST /api/matches/{id}/goals (vínculo manual de gols na página da partida).
    private static bool IsPublicGoalWrite(PathString path, string method)
    {
        if (path.StartsWithSegments(GoalRegistrationsRoot))
            return HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsDelete(method);

        if (HttpMethods.IsPost(method) && path.StartsWithSegments(MatchesRoot, out var rest))
        {
            // rest = "/{id}/goals"
            var segments = (rest.Value ?? string.Empty).Trim('/').Split('/');
            return segments.Length == 2
                && long.TryParse(segments[0], out _)
                && string.Equals(segments[1], "goals", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}
