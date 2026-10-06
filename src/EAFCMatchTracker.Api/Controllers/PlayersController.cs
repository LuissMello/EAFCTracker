using EAFCMatchTracker.Application.Dtos;
using EAFCMatchTracker.Application.Interfaces.Services;
using EAFCMatchTracker.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace EAFCMatchTracker.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PlayersController : ControllerBase
{
    private readonly IPlayerService _playerService;
    private readonly ILogger<PlayersController> _logger;

    public PlayersController(IPlayerService playerService, ILogger<PlayersController> logger)
    {
        _playerService = playerService;
        _logger = logger;
    }

    [HttpGet("{playerId:long}")]
    public async Task<ActionResult<PlayerDto>> GetPlayerById(long playerId, CancellationToken ct)
    {
        _logger.LogInformation("GetPlayerById called with playerId: {PlayerId}", playerId);
        try
        {
            var player = await _playerService.GetByIdAsync(playerId, ct);
            if (player is null)
            {
                _logger.LogWarning("Player not found. playerId: {PlayerId}", playerId);
                return NotFound();
            }
            _logger.LogInformation("Player found. playerId: {PlayerId}", playerId);
            return Ok(player);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred in GetPlayerById for playerId: {PlayerId}", playerId);
            return StatusCode(500, "An error occurred while processing your request.");
        }
    }

    [HttpGet("{playerEntityId:long}/profile")]
    public async Task<ActionResult<PlayerProfileDto>> GetPlayerProfile(
        long playerEntityId, CancellationToken ct, [FromQuery] string? archetypeId = null, [FromQuery] string? positionGroup = null)
    {
        // filtros opcionais do histórico: archetypeId (1..255) e positionGroup (ATAQUE/MEIO/DEFESA/GOLEIRO)
        int? archetype = null;
        if (!string.IsNullOrWhiteSpace(archetypeId))
        {
            if (!int.TryParse(archetypeId.Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var a)
                || a < 1 || a > 255)
                return BadRequest(new ProblemDetails { Status = 400, Title = "Requisição inválida", Detail = "archetypeId deve ser um número inteiro entre 1 e 255." });
            archetype = a;
        }
        string? group = null;
        if (!string.IsNullOrWhiteSpace(positionGroup))
        {
            group = EAFCMatchTracker.Application.Services.Analytics.ArchetypeGroups.Normalize(positionGroup);
            if (group is null)
                return BadRequest(new ProblemDetails
                {
                    Status = 400, Title = "Requisição inválida",
                    Detail = $"positionGroup deve ser um de: {string.Join(", ", EAFCMatchTracker.Application.Services.Analytics.ArchetypeGroups.All)}."
                });
        }

        _logger.LogInformation("GetPlayerProfile called for playerEntityId={PlayerEntityId}", playerEntityId);
        try
        {
            var profile = await _playerService.GetProfileAsync(playerEntityId, archetype, group, ct);
            if (profile is null) return NotFound();
            return Ok(profile);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetPlayerProfile for playerEntityId={PlayerEntityId}", playerEntityId);
            return StatusCode(500, "Erro interno ao buscar perfil do jogador.");
        }
    }
}
