using EAFCMatchTracker.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EAFCMatchTracker.Api.Controllers;

/// <summary>Retrospectiva ("wrapped") por edição do jogo. Público, somente leitura, com rate limit por IP.</summary>
[Route("api/clubs/{clubId:long}/wrapped")]
[EnableRateLimiting(RateLimitPolicy)]
public class WrappedController : AnalyticsControllerBase
{
    private readonly IWrappedService _service;

    public WrappedController(IWrappedService service) => _service = service;

    // GET /api/clubs/{clubId}/wrapped?gameVersion=27
    [HttpGet]
    public async Task<IActionResult> Get(long clubId, [FromQuery] string? gameVersion, CancellationToken ct)
    {
        if (ValidateClub(clubId) is { } bad) return bad;
        if (ParseGameVersion(gameVersion, out var version) is { } badVersion) return badVersion;
        return Ok(await _service.GetWrappedAsync(clubId, version, ct));
    }
}
