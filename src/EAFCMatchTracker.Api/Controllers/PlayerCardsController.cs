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

    // GET /api/clubs/{clubId}/player-cards?from=&to=&gameVersion=&minMatches=3&archetypeId=&positionGroup=&archetypeA=&archetypeB=
    [HttpGet("player-cards")]
    public async Task<IActionResult> Cards(
        long clubId, [FromQuery] string? from, [FromQuery] string? to, [FromQuery] string? gameVersion,
        [FromQuery] string? minMatches, CancellationToken ct, [FromQuery] string? archetypeId = null, [FromQuery] string? positionGroup = null, [FromQuery] string? view = null,
        [FromQuery] string? playerEntityId = null)
    {
        if (ValidateClub(clubId) is { } bad) return bad;
        if (ParseRange(from, to, out var f, out var t) is { } badRange) return badRange;
        if (ParseGameVersion(gameVersion, out var version) is { } badVersion) return badVersion;
        if (ParseMinMatches(minMatches, 3, out var min) is { } badMin) return badMin;
        if (ParseArchetypeId(archetypeId, out var archetype) is { } badArchetype) return badArchetype;
        if (ParsePositionGroup(positionGroup, out var group) is { } badGroup) return badGroup;
        var viewName = "player";
        if (!string.IsNullOrWhiteSpace(view))
        {
            viewName = view.Trim().ToLowerInvariant();
            if (viewName is not ("player" or "archetype")) return BadInput("view deve ser 'player' ou 'archetype'.");
        }
        long? player = null;
        if (!string.IsNullOrWhiteSpace(playerEntityId))
        {
            if (!TryParseId(playerEntityId, out var pid)) return BadInput("playerEntityId deve ser o id (inteiro positivo) de um jogador.");
            player = pid;
        }
        return Ok(await _service.GetCardsAsync(clubId, f, t, version, min, archetype, group, viewName, player, ct));
    }

    // GET /api/clubs/{clubId}/player-compare?a=&b=&from=&to=&gameVersion=&archetypeId=&positionGroup=
    [HttpGet("player-compare")]
    public async Task<IActionResult> Compare(
        long clubId, [FromQuery] string? a, [FromQuery] string? b, [FromQuery] string? from, [FromQuery] string? to,
        [FromQuery] string? gameVersion, CancellationToken ct, [FromQuery] string? archetypeId = null,
        [FromQuery] string? positionGroup = null, [FromQuery] string? archetypeA = null, [FromQuery] string? archetypeB = null)
    {
        if (ValidateClub(clubId) is { } bad) return bad;
        if (!TryParseId(a, out var idA)) return BadInput("a deve ser o id (inteiro positivo) de um jogador.");
        if (!TryParseId(b, out var idB)) return BadInput("b deve ser o id (inteiro positivo) de um jogador.");
        if (ParseRange(from, to, out var f, out var t) is { } badRange) return badRange;
        if (ParseGameVersion(gameVersion, out var version) is { } badVersion) return badVersion;

        if (ParseArchetypeId(archetypeId, out var archetype) is { } badArchetype) return badArchetype;

        if (ParsePositionGroup(positionGroup, out var group) is { } badGroup) return badGroup;

        if (ParseArchetypeId(archetypeA, out var archA) is { } badA) return BadInput("archetypeA deve ser um número inteiro entre 1 e 255.");
        if (ParseArchetypeId(archetypeB, out var archB) is { } badB) return BadInput("archetypeB deve ser um número inteiro entre 1 e 255.");

        // O mesmo jogador só pode estar dos dois lados se for comparado COMO arquétipos diferentes (archetypeA/archetypeB).
        if (idA == idB && (archA ?? archetype) == (archB ?? archetype)) return BadInput("Escolha dois jogadores diferentes para comparar.");

        var result = await _service.GetCompareAsync(clubId, idA, idB, f, t, version, archetype, archA, archB, group, ct);
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
