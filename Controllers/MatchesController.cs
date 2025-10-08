    using EAFCMatchTracker.Dtos;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;

    namespace EAFCMatchTracker.Controllers;

    [ApiController]
    [Route("api/[controller]")]
    public class MatchesController : ControllerBase
    {
        private readonly EAFCContext _db;
        private readonly ILogger<MatchesController> _logger;

        public MatchesController(EAFCContext dbContext, ILogger<MatchesController> logger)
        {
            _db = dbContext;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<List<MatchDto>>> GetAllMatches(CancellationToken ct)
        {
            try
            {
                _logger.LogInformation("Getting all matches.");
                var matches = await _db.Matches
                    .AsNoTracking()
                    .OrderByDescending(m => m.Timestamp)
                    .ToMatchDtoListAsync(ct);
                _logger.LogInformation("Successfully retrieved all matches.");
                return Ok(matches);
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
                var dto = await _db.Matches
                    .AsNoTracking()
                    .Where(m => m.MatchId == matchId)
                    .FirstMatchDtoOrDefaultAsync(ct);

                if (dto is null)
                {
                    _logger.LogWarning("Match not found: {MatchId}", matchId);
                    return NotFound();
                }
                _logger.LogInformation("Successfully retrieved match: {MatchId}", matchId);
                return Ok(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while getting match by id: {MatchId}", matchId);
                return StatusCode(500, "Internal server error.");
            }
        }

    [HttpGet("{matchId:long}/statistics")]
    public async Task<IActionResult> GetMatchStatisticsById(long matchId, CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Getting statistics for match: {MatchId}", matchId);
            var match = await _db.Matches
                .AsNoTracking()
                .Include(m => m.Clubs).ThenInclude(c => c.Details)
                .Include(m => m.MatchPlayers).ThenInclude(mp => mp.Player)
                .FirstOrDefaultAsync(m => m.MatchId == matchId, ct);

            if (match == null)
            {
                _logger.LogWarning("Match not found for statistics: {MatchId}", matchId);
                return NotFound();
            }

            var overall = StatsAggregator.BuildOverallForSingleMatch(match.MatchPlayers);  
            var playersStats = StatsAggregator.BuildPerPlayer(match.MatchPlayers, includeDisconnected: true); 
            var clubsStats = StatsAggregator.BuildPerClub(
                match.MatchPlayers,
                match.Clubs.ToDictionary(c => c.ClubId)
            );

            _logger.LogInformation("Successfully retrieved statistics for match: {MatchId}", matchId);
            return Ok(new
            {
                Overall = overall,
                Players = playersStats,
                Clubs = clubsStats,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while getting statistics for match: {MatchId}", matchId);
            return StatusCode(500, "Internal server error.");
        }
    }


    [HttpGet("{matchId:long}/players/{playerId:long}/statistics")]
        public async Task<ActionResult<MatchPlayerStatsDto>> GetPlayerStatisticsByMatchAndPlayer(long matchId, long playerId, CancellationToken ct)
        {
            try
            {
                _logger.LogInformation("Getting player statistics for match: {MatchId}, player: {PlayerId}", matchId, playerId);
                var dto = await _db.MatchPlayers
                    .AsNoTracking()
                    .Where(mp => mp.MatchId == matchId && mp.PlayerEntityId == playerId)
                    .ProjectPlayerStats()
                    .FirstOrDefaultAsync(ct);

                if (dto is null)
                {
                    _logger.LogWarning("Player statistics not found for match: {MatchId}, player: {PlayerId}", matchId, playerId);
                    return NotFound();
                }
                _logger.LogInformation("Successfully retrieved player statistics for match: {MatchId}, player: {PlayerId}", matchId, playerId);
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
                var match = await _db.Matches
                    .Include(m => m.MatchPlayers)
                    .Include(m => m.Clubs)
                    .FirstOrDefaultAsync(m => m.MatchId == matchId, ct);

                if (match == null)
                {
                    _logger.LogWarning("Match not found for deletion: {MatchId}", matchId);
                    return NotFound(new { message = "Partida não encontrada" });
                }

                var matchPlayers = await _db.MatchPlayers
                    .Where(mp => mp.MatchId == matchId)
                    .ToListAsync(ct);

                var statsIds = matchPlayers.Select(mp => mp.PlayerMatchStatsEntityId).ToList();
                var playerMatchStats = await _db.PlayerMatchStats
                    .Where(pms => statsIds.Contains(pms.Id))
                    .ToListAsync(ct);

                _db.PlayerMatchStats.RemoveRange(playerMatchStats);
                _db.MatchPlayers.RemoveRange(matchPlayers);

                var matchClubs = await _db.MatchClubs.Where(mc => mc.MatchId == matchId).ToListAsync(ct);
                _db.MatchClubs.RemoveRange(matchClubs);

                _db.Matches.Remove(match);
                await _db.SaveChangesAsync(ct);

                _logger.LogInformation("Successfully deleted match: {MatchId}", matchId);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while deleting match: {MatchId}", matchId);
                return StatusCode(500, "Internal server error.");
            }
        }
    }
