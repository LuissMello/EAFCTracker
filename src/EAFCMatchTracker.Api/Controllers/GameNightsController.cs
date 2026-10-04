using EAFCMatchTracker.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EAFCMatchTracker.Api.Controllers;

/// <summary>Noites de jogo (sessões do clube). Público, somente leitura, com rate limit por IP.</summary>
[Route("api/clubs/{clubId:long}/game-nights")]
[EnableRateLimiting(RateLimitPolicy)]
public class GameNightsController : AnalyticsControllerBase
{
    private readonly IGameNightService _service;

    public GameNightsController(IGameNightService service) => _service = service;

    // GET /api/clubs/{clubId}/game-nights?gameVersion=27
    [HttpGet]
    public async Task<IActionResult> List(long clubId, [FromQuery] string? gameVersion, CancellationToken ct)
    {
        if (ValidateClub(clubId) is { } bad) return bad;
        if (ParseGameVersion(gameVersion, out var version) is { } badVersion) return badVersion;
        return Ok(await _service.GetNightsAsync(clubId, version, ct));
    }

    // GET /api/clubs/{clubId}/game-nights/{sessionId}[?gameVersion=27]  (gameVersion só afeta anterior/próxima/índice)
    [HttpGet("{sessionId:long}")]
    public async Task<IActionResult> Detail(long clubId, long sessionId, [FromQuery] string? gameVersion, CancellationToken ct)
    {
        if (ValidateClub(clubId) is { } bad) return bad;
        if (ParseGameVersion(gameVersion, out var version) is { } badVersion) return badVersion;
        var night = await _service.GetNightAsync(clubId, sessionId, version, ct);
        return night is null
            ? NotFoundProblem("Noite de jogo não encontrada para este clube.")
            : Ok(night);
    }
}
