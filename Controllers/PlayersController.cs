using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EAFCMatchTracker.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PlayersController : ControllerBase
{
    private readonly EAFCContext _db;
    private readonly ILogger<PlayersController> _logger;

    public PlayersController(EAFCContext dbContext, ILogger<PlayersController> logger)
    {
        _db = dbContext;
        _logger = logger;
    }

    [HttpGet("{playerId:long}")]
    public async Task<ActionResult<PlayerEntity>> GetPlayerById(long playerId, CancellationToken ct)
    {
        _logger.LogInformation("GetPlayerById called with playerId: {PlayerId}", playerId);
        try
        {
            var player = await _db.Players.AsNoTracking().FirstOrDefaultAsync(p => p.PlayerId == playerId, ct);
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
}
