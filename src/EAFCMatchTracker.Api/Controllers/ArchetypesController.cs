using EAFCMatchTracker.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EAFCMatchTracker.Api.Controllers;

/// <summary>Arquétipos de jogador: catálogo e resumo por clube. Público, somente leitura, com rate limit por IP.</summary>
[Route("api")]
[EnableRateLimiting(RateLimitPolicy)]
public class ArchetypesController : AnalyticsControllerBase
{
    private readonly IArchetypeService _service;

    public ArchetypesController(IArchetypeService service) => _service = service;

    // GET /api/archetypes  -> catálogo + ids observados (cache de 10 min)
    [HttpGet("archetypes")]
    public async Task<IActionResult> Catalog(CancellationToken ct) => Ok(await _service.GetCatalogAsync(ct));

    // GET /api/clubs/{clubId}/archetypes/summary?from=&to=&gameVersion=&archetypeId=&positionGroup=
    [HttpGet("clubs/{clubId:long}/archetypes/summary")]
    public async Task<IActionResult> Summary(
        long clubId, [FromQuery] string? from, [FromQuery] string? to, [FromQuery] string? gameVersion, CancellationToken ct,
        [FromQuery] string? archetypeId = null, [FromQuery] string? positionGroup = null)
    {
        if (ValidateClub(clubId) is { } bad) return bad;
        if (ParseRange(from, to, out var f, out var t) is { } badRange) return badRange;
        if (ParseGameVersion(gameVersion, out var version) is { } badVersion) return badVersion;
        if (ParseArchetypeId(archetypeId, out var archetype) is { } badArchetype) return badArchetype;
        if (ParsePositionGroup(positionGroup, out var group) is { } badGroup) return badGroup;
        return Ok(await _service.GetSummaryAsync(clubId, f, t, version, archetype, group, ct));
    }
}
