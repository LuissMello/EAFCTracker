using EAFCMatchTracker.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace EAFCMatchTracker.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TrendsController : ControllerBase
{
    private readonly ITrendsService _trendsService;
    private readonly ILogger<TrendsController> _logger;

    public TrendsController(ITrendsService trendsService, ILogger<TrendsController> logger)
    {
        _trendsService = trendsService;
        _logger = logger;
    }

    [HttpGet("club/{clubId:long}")]
    public async Task<IActionResult> GetClubTrends(
        long clubId,
        [FromQuery] int last = 30,
        [FromQuery] DateTime? since = null,
        [FromQuery] DateTime? until = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("GetClubTrends called for ClubId={ClubId}, last={Last}, since={Since}, until={Until}", clubId, last, since, until);
        try
        {
            var result = await _trendsService.GetClubTrendsAsync(clubId, last, since, until, ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetClubTrends for ClubId={ClubId}", clubId);
            return StatusCode(500, "An error occurred while processing your request.");
        }
    }

    [HttpGet("top-scorers")]
    public async Task<IActionResult> GetTopScorers(
        [FromQuery] long clubId,
        [FromQuery] DateTime? since = null,
        [FromQuery] DateTime? until = null,
        [FromQuery] int limit = 10,
        CancellationToken ct = default)
    {
        _logger.LogInformation("GetTopScorers called for ClubId={ClubId}, since={Since}, until={Until}, limit={Limit}", clubId, since, until, limit);
        try
        {
            var result = await _trendsService.GetTopScorersAsync(clubId, since, until, limit, ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetTopScorers for ClubId={ClubId}", clubId);
            return StatusCode(500, "An error occurred while processing your request.");
        }
    }
}
