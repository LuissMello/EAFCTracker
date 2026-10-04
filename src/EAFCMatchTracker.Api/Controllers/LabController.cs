using EAFCMatchTracker.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EAFCMatchTracker.Api.Controllers;

/// <summary>Laboratório "Com e Sem". Público, somente leitura, com rate limit por IP. Correlação, não causa.</summary>
[Route("api/clubs/{clubId:long}/lab")]
[EnableRateLimiting(RateLimitPolicy)]
public class LabController : AnalyticsControllerBase
{
    private readonly ILabService _service;

    public LabController(ILabService service) => _service = service;

    // GET /api/clubs/{clubId}/lab/player-impact?from=&to=&gameVersion=&minMatches=3
    [HttpGet("player-impact")]
    public async Task<IActionResult> PlayerImpact(
        long clubId, [FromQuery] string? from, [FromQuery] string? to, [FromQuery] string? gameVersion,
        [FromQuery] string? minMatches, CancellationToken ct)
    {
        if (ValidateClub(clubId) is { } bad) return bad;
        if (ParseRange(from, to, out var f, out var t) is { } badRange) return badRange;
        if (ParseGameVersion(gameVersion, out var version) is { } badVersion) return badVersion;
        if (ParseMinMatches(minMatches, 3, out var min) is { } badMin) return badMin;
        return Ok(await _service.GetPlayerImpactAsync(clubId, f, t, version, min, ct));
    }

    // GET /api/clubs/{clubId}/lab/context?from=&to=&gameVersion=
    [HttpGet("context")]
    public async Task<IActionResult> Context(
        long clubId, [FromQuery] string? from, [FromQuery] string? to, [FromQuery] string? gameVersion, CancellationToken ct)
    {
        if (ValidateClub(clubId) is { } bad) return bad;
        if (ParseRange(from, to, out var f, out var t) is { } badRange) return badRange;
        if (ParseGameVersion(gameVersion, out var version) is { } badVersion) return badVersion;
        return Ok(await _service.GetContextAsync(clubId, f, t, version, ct));
    }

    // GET /api/clubs/{clubId}/lab/duos?from=&to=&gameVersion=&minMatches=5
    [HttpGet("duos")]
    public async Task<IActionResult> Duos(
        long clubId, [FromQuery] string? from, [FromQuery] string? to, [FromQuery] string? gameVersion,
        [FromQuery] string? minMatches, CancellationToken ct)
    {
        if (ValidateClub(clubId) is { } bad) return bad;
        if (ParseRange(from, to, out var f, out var t) is { } badRange) return badRange;
        if (ParseGameVersion(gameVersion, out var version) is { } badVersion) return badVersion;
        if (ParseMinMatches(minMatches, 5, out var min) is { } badMin) return badMin;
        return Ok(await _service.GetDuosAsync(clubId, f, t, version, min, ct));
    }
}
