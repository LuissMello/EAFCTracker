using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EAFCMatchTracker.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PlayersController : ControllerBase
{
    private readonly EAFCContext _db;

    public PlayersController(EAFCContext dbContext) => _db = dbContext;

    [HttpGet("{playerId:long}")]
    public async Task<ActionResult<PlayerEntity>> GetPlayerById(long playerId, CancellationToken ct)
    {
        var player = await _db.Players.AsNoTracking().FirstOrDefaultAsync(p => p.PlayerId == playerId, ct);
        return player is null ? NotFound() : Ok(player);
    }
}
