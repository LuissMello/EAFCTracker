using EAFCMatchTracker.Application.Dtos;
using EAFCMatchTracker.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace EAFCMatchTracker.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MaintenanceController : ControllerBase
{
    private readonly IMaintenanceService _maintenanceService;
    private readonly ILogger<MaintenanceController> _logger;

    public MaintenanceController(IMaintenanceService maintenanceService, ILogger<MaintenanceController> logger)
    {
        _maintenanceService = maintenanceService;
        _logger = logger;
    }

    [HttpPost("clubs/overall/refresh")]
    public async Task<IActionResult> RefreshClubsOverall(CancellationToken ct)
    {
        try
        {
            var result = await _maintenanceService.RefreshClubsOverallAsync(ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in RefreshClubsOverall");
            return StatusCode(500, new { error = "Ocorreu um erro ao atualizar os clubes.", details = ex.Message });
        }
    }

    [HttpPost("club/{clubId:long}/division/refresh")]
    public async Task<IActionResult> RefreshClubCurrentDivision(long clubId, [FromQuery] string? name, CancellationToken ct)
    {
        try
        {
            if (clubId <= 0) return BadRequest("Informe um clubId válido.");

            var result = await _maintenanceService.RefreshClubCurrentDivisionAsync(clubId, name, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in RefreshClubCurrentDivision for clubId={ClubId}", clubId);
            return StatusCode(500, new { error = "Ocorreu um erro ao atualizar a divisão do clube.", details = ex.Message });
        }
    }

    [HttpPost("club/{clubId:long}/members/enrich")]
    public async Task<IActionResult> EnrichMatchPlayersWithMembers(long clubId, CancellationToken ct)
    {
        try
        {
            if (clubId <= 0) return BadRequest("Informe um clubId válido.");

            var result = await _maintenanceService.EnrichMatchPlayersWithMembersAsync(clubId, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in EnrichMatchPlayersWithMembers for clubId={ClubId}", clubId);
            return StatusCode(500, new { error = "Ocorreu um erro ao enriquecer os jogadores do clube.", details = ex.Message });
        }
    }

    [HttpPost("club/{clubId:long}/refresh-external")]
    public async Task<IActionResult> RefreshClubExternal(long clubId, [FromQuery] string? name, CancellationToken ct)
    {
        try
        {
            if (clubId <= 0) return BadRequest("Informe um clubId válido.");

            var divResult = await _maintenanceService.RefreshClubCurrentDivisionAsync(clubId, name, ct);
            var enrichResult = await _maintenanceService.EnrichMatchPlayersWithMembersAsync(clubId, ct);

            return Ok(new { division = divResult, members = enrichResult });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in RefreshClubExternal for clubId={ClubId}", clubId);
            return StatusCode(500, new { error = "Ocorreu um erro ao atualizar dados externos do clube.", details = ex.Message });
        }
    }

    [HttpPost("club/{clubId:long}/opponents/division/refresh")]
    public async Task<IActionResult> RefreshOpponentsCurrentDivision(long clubId, CancellationToken ct)
    {
        try
        {
            if (clubId <= 0) return BadRequest("Informe um clubId válido.");

            var result = await _maintenanceService.RefreshOpponentsCurrentDivisionAsync(clubId, ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in RefreshOpponentsCurrentDivision for clubId={ClubId}", clubId);
            return StatusCode(500, new { error = "Ocorreu um erro ao atualizar divisões dos oponentes.", details = ex.Message });
        }
    }

    [HttpPost("clubs/playoffs/refresh-all")]
    public async Task<IActionResult> RefreshAllPlayoffsAchievements(CancellationToken ct)
    {
        try
        {
            var result = await _maintenanceService.RefreshAllPlayoffsAchievementsAsync(ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in RefreshAllPlayoffsAchievements");
            return StatusCode(500, new { error = "Ocorreu um erro ao atualizar os playoffs.", details = ex.Message });
        }
    }

    [HttpPost("clubs/overall/refresh-all")]
    public async Task<IActionResult> RefreshAllOverallStats(CancellationToken ct)
    {
        try
        {
            var result = await _maintenanceService.RefreshAllOverallStatsAsync(ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in RefreshAllOverallStats");
            return StatusCode(500, new { error = "Ocorreu um erro ao atualizar os overall stats.", details = ex.Message });
        }
    }

    [HttpPost("clubs/division/refresh-all")]
    public async Task<IActionResult> RefreshAllCurrentDivisions(CancellationToken ct)
    {
        try
        {
            var result = await _maintenanceService.RefreshAllCurrentDivisionsAsync(ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in RefreshAllCurrentDivisions");
            return StatusCode(500, new { error = "Ocorreu um erro ao atualizar as divisões dos clubes.", details = ex.Message });
        }
    }

    [HttpPost("clubs/members/enrich-all")]
    public async Task<IActionResult> EnrichAllMatchPlayersWithMembers(CancellationToken ct)
    {
        try
        {
            var result = await _maintenanceService.EnrichAllMatchPlayersWithMembersAsync(ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in EnrichAllMatchPlayersWithMembers");
            return StatusCode(500, new { error = "Ocorreu um erro ao enriquecer os jogadores dos clubes.", details = ex.Message });
        }
    }

    [HttpPost("clubs/refresh-everything")]
    public async Task<IActionResult> RefreshEverything(CancellationToken ct)
    {
        try
        {
            var result = await _maintenanceService.RefreshEverythingAsync(ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in RefreshEverything");
            return StatusCode(500, new { error = "Ocorreu um erro ao atualizar tudo.", details = ex.Message });
        }
    }

    [HttpGet("matches/recent-aggregates")]
    public async Task<ActionResult<RecentMatchesWithAggregatesDto>> GetRecentMatchesWithAggregates(
        [FromQuery] int count = 10,
        CancellationToken ct = default)
    {
        try
        {
            const int MaxCount = 200;
            if (count < 1) count = 1;
            if (count > MaxCount) count = MaxCount;

            var result = await _maintenanceService.GetRecentMatchesWithAggregatesAsync(count, ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetRecentMatchesWithAggregates");
            return StatusCode(500, "Erro interno ao buscar partidas recentes.");
        }
    }

    [HttpGet("myip")]
    public async Task<string> GetMyIp()
    {
        using var http = new HttpClient();
        return await http.GetStringAsync("https://api.ipify.org");
    }
}
