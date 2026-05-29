using EAFCMatchTracker.Application.Dtos;
using EAFCMatchTracker.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace EAFCMatchTracker.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MatchesController : ControllerBase
{
    private readonly IMatchService _matchService;
    private readonly IGoalAnalysisService _goalAnalysisService;
    private readonly ILogger<MatchesController> _logger;

    public MatchesController(
        IMatchService matchService,
        IGoalAnalysisService goalAnalysisService,
        ILogger<MatchesController> logger)
    {
        _matchService = matchService;
        _goalAnalysisService = goalAnalysisService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<MatchDto>>> GetAllMatches(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Getting all matches. Page={Page}, PageSize={PageSize}", page, pageSize);

            if (page < 1) page = 1;
            const int MaxPageSize = 200;
            if (pageSize < 1) pageSize = 50;
            if (pageSize > MaxPageSize) pageSize = MaxPageSize;

            var result = await _matchService.GetAllMatchesAsync(page, pageSize, ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while getting all matches.");
            return StatusCode(500, "Internal server error.");
        }
    }

    [HttpGet("{matchId:long}")]
    public async Task<ActionResult<MatchDto>> GetMatchById(long matchId, CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Getting match by id: {MatchId}", matchId);
            var dto = await _matchService.GetMatchByIdAsync(matchId, ct);
            if (dto is null)
            {
                _logger.LogWarning("Match not found: {MatchId}", matchId);
                return NotFound();
            }
            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while getting match by id: {MatchId}", matchId);
            return StatusCode(500, "Internal server error.");
        }
    }

    [HttpGet("{matchId:long}/statistics")]
    public async Task<ActionResult<MatchStatisticsResponseDto>> GetMatchStatisticsById(long matchId, CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Getting statistics for match: {MatchId}", matchId);
            var result = await _matchService.GetMatchStatisticsByIdAsync(matchId, ct);
            if (result is null) return NotFound();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while getting statistics for match: {MatchId}", matchId);
            return StatusCode(500, "Internal server error.");
        }
    }

    [HttpGet("{matchId:long}/event-aggregates")]
    public async Task<ActionResult<MatchEventAggregatesResponseDto>> GetMatchEventAggregates(long matchId, CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Getting event aggregates for match: {MatchId}", matchId);
            var result = await _matchService.GetMatchEventAggregatesAsync(matchId, ct);
            if (result is null) return NotFound();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while getting event aggregates for match: {MatchId}", matchId);
            return StatusCode(500, "Internal server error.");
        }
    }

    [HttpGet("{matchId:long}/players/{playerId:long}/statistics")]
    public async Task<ActionResult<MatchPlayerStatsDto>> GetPlayerStatisticsByMatchAndPlayer(long matchId, long playerId, CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Getting player statistics for match: {MatchId}, player: {PlayerId}", matchId, playerId);
            var dto = await _matchService.GetPlayerStatisticsByMatchAndPlayerAsync(matchId, playerId, ct);
            if (dto is null) return NotFound();
            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while getting player statistics for match: {MatchId}, player: {PlayerId}", matchId, playerId);
            return StatusCode(500, "Internal server error.");
        }
    }

    [HttpDelete("{matchId:long}")]
    public async Task<IActionResult> DeleteMatch(long matchId, CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Deleting match: {MatchId}", matchId);
            await _matchService.DeleteMatchAsync(matchId, ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            _logger.LogWarning("Match not found for deletion: {MatchId}", matchId);
            return NotFound(new { message = "Partida não encontrada" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while deleting match: {MatchId}", matchId);
            return StatusCode(500, "Internal server error.");
        }
    }

    [HttpPost("{matchId}/goals")]
    public async Task<IActionResult> RegisterGoals(long matchId, [FromBody] RegisterGoalsRequest request, CancellationToken ct)
    {
        if (request.Goals == null || request.Goals.Count == 0)
            return BadRequest("No goals provided.");

        try
        {
            await _goalAnalysisService.RegisterGoalsAsync(matchId, request, ct);
            return Ok(new { message = "Goals registered successfully." });
        }
        catch (KeyNotFoundException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while registering goals for match: {MatchId}", matchId);
            return StatusCode(500, "Internal server error.");
        }
    }

    [HttpGet("{matchId}/goals")]
    public async Task<ActionResult<MatchGoalsResponseDto>> GetGoalsByMatchId(long matchId, CancellationToken ct)
    {
        var result = await _goalAnalysisService.GetGoalsByMatchIdAsync(matchId, ct);
        if (result is null) return NotFound();
        return Ok(result);
    }
}
