using EAFCMatchTracker.Api.Security;
using Microsoft.AspNetCore.Mvc;

namespace EAFCMatchTracker.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private const int MaxPasswordLength = 512;
    private static readonly TimeSpan FailedAttemptDelay = TimeSpan.FromMilliseconds(400);

    private readonly IAdminTokenService _tokens;
    private readonly ILoginAttemptTracker _attempts;
    private readonly bool _behindFlyProxy;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAdminTokenService tokens,
        ILoginAttemptTracker attempts,
        IConfiguration config,
        ILogger<AuthController> logger)
    {
        _tokens = tokens;
        _attempts = attempts;
        _behindFlyProxy = !string.IsNullOrEmpty(config["FLY_APP_NAME"]);
        _logger = logger;
    }

    // POST /api/auth/login   body: { password }   (público; a checagem é feita aqui)
    [HttpPost("login")]
    [RequestSizeLimit(4096)]
    public async Task<IActionResult> Login([FromBody] LoginRequest body, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";

        if (!_tokens.IsConfigured)
        {
            return Fail(StatusCodes.Status503ServiceUnavailable, "Service Unavailable",
                "Autenticação de administrador não configurada.", "auth_not_configured");
        }

        var client = ClientIpResolver.Resolve(HttpContext, _behindFlyProxy);

        if (_attempts.IsLockedOut(client, out var retryAfter))
            return TooManyAttempts(retryAfter);

        var password = body.Password;
        if (password is { Length: <= MaxPasswordLength } && _tokens.VerifyPassword(password))
        {
            _attempts.Reset(client);
            var issued = _tokens.Issue();
            _logger.LogInformation("Login de administrador bem-sucedido. IP: {ClientIp}", client);
            return Ok(new { token = issued.Token, expiresAtUtc = issued.ExpiresAtUtc.UtcDateTime });
        }

        // Nunca registra a senha informada; apenas o IP
        var lockedNow = _attempts.RegisterFailure(client);
        _logger.LogWarning("Falha de login de administrador. IP: {ClientIp}", client);

        await Task.Delay(FailedAttemptDelay, ct);

        // A tentativa que dispara o bloqueio ainda recebe 401; as seguintes recebem 429.
        if (lockedNow)
            _logger.LogWarning("Muitas falhas de login; IP bloqueado temporariamente. IP: {ClientIp}", client);

        return Fail(StatusCodes.Status401Unauthorized, "Unauthorized", "Senha inválida.", "invalid_credentials");
    }

    private IActionResult TooManyAttempts(TimeSpan retryAfter)
    {
        var seconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
        Response.Headers.RetryAfter = seconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return Fail(StatusCodes.Status429TooManyRequests, "Too Many Requests",
            "Muitas tentativas de login. Tente novamente mais tarde.", "too_many_attempts");
    }

    private ObjectResult Fail(int status, string title, string detail, string code)
    {
        var problem = new ProblemDetails { Status = status, Title = title, Detail = detail };
        problem.Extensions["code"] = code;
        return new ObjectResult(problem)
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" }
        };
    }
}

public record LoginRequest(string? Password);
