using EAFCMatchTracker.Application.Interfaces.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace EAFCMatchTracker.Api.Controllers;

/// <summary>Edições do jogo (FC25, FC26, FC27...). Leitura pública; a administração fica em /api/admin/game-versions.</summary>
[ApiController]
[Route("api/game-versions")]
public class GameVersionsController : ControllerBase
{
    private readonly IGameVersionRepository _versions;

    public GameVersionsController(IGameVersionRepository versions)
    {
        _versions = versions;
    }

    // GET /api/game-versions -> [{ id, version, name, startsAt, isCurrent }]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<GameVersionDto>>> GetAll(CancellationToken ct)
    {
        var all = await _versions.GetAllAsync(ct);
        return Ok(all.Select(GameVersionDto.From));
    }
}
