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
    public async Task<ActionResult<PlayerEntity>> GetPlayerById(long playerId, CancellationToken ct)
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
    public async Task<ActionResult<PlayerProfileDto>> GetPlayerProfile(long playerEntityId, CancellationToken ct)
    {
        _logger.LogInformation("GetPlayerProfile called for playerEntityId={PlayerEntityId}", playerEntityId);
        try
        {
            var profile = await _playerService.GetProfileAsync(playerEntityId, ct);
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
