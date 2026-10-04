using System.Globalization;
using EAFCMatchTracker.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EAFCMatchTracker.Api.Controllers;

/// <summary>Cartas dos jogadores (estilo FUT) e comparador de jogadores. Público, somente leitura, com rate limit por IP.</summary>
[Route("api/clubs/{clubId:long}")]
[EnableRateLimiting(RateLimitPolicy)]
public class PlayerCardsController : AnalyticsControllerBase
{
    private readonly IPlayerCardService _service;

    public PlayerCardsController(IPlayerCardService service) => _service = service;

    // GET /api/clubs/{clubId}/player-cards?from=&to=&gameVersion=&minMatches=3
    [HttpGet("player-cards")]
    public async Task<IActionResult> Cards(
        long clubId, [FromQuery] string? from, [FromQuery] string? to, [FromQuery] string? gameVersion,
        [FromQuery] string? minMatches, CancellationToken ct)
    {
        if (ValidateClub(clubId) is { } bad) return bad;
        if (ParseRange(from, to, out var f, out var t) is { } badRange) return badRange;
        if (ParseGameVersion(gameVersion, out var version) is { } badVersion) return badVersion;
        if (ParseMinMatches(minMatches, 3, out var min) is { } badMin) return badMin;
        return Ok(await _service.GetCardsAsync(clubId, f, t, version, min, ct));
    }

    // GET /api/clubs/{clubId}/player-compare?a=&b=&from=&to=&gameVersion=
    [HttpGet("player-compare")]
    public async Task<IActionResult> Compare(
        long clubId, [FromQuery] string? a, [FromQuery] string? b, [FromQuery] string? from, [FromQuery] string? to,
        [FromQuery] string? gameVersion, CancellationToken ct)
    {
        if (ValidateClub(clubId) is { } bad) return bad;
        if (!TryParseId(a, out var idA)) return BadInput("a deve ser o id (inteiro positivo) de um jogador.");
        if (!TryParseId(b, out var idB)) return BadInput("b deve ser o id (inteiro positivo) de um jogador.");
        if (idA == idB) return BadInput("Escolha dois jogadores diferentes para comparar.");
        if (ParseRange(from, to, out var f, out var t) is { } badRange) return badRange;
        if (ParseGameVersion(gameVersion, out var version) is { } badVersion) return badVersion;

        var result = await _service.GetCompareAsync(clubId, idA, idB, f, t, version, ct);
        return result is null
            ? NotFoundProblem("Jogador não encontrado neste clube.")
            : Ok(result);
    }

    private static bool TryParseId(string? raw, out long id)
    {
        id = 0;
        return !string.IsNullOrWhiteSpace(raw)
               && long.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out id)
               && id > 0;
    }
}
