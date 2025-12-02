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
    public async Task<ActionResult<MatchStatisticsResponseDto>> GetMatchStatisticsById(long matchId, CancellationToken ct)
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

            var response = new MatchStatisticsResponseDto
            {
                Overall = overall,
                Players = playersStats,
                Clubs = clubsStats
            };

            _logger.LogInformation("Successfully retrieved statistics for match: {MatchId}", matchId);
            return Ok(response);
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

    [HttpPost("{matchId}/goals")]
    public async Task<IActionResult> RegisterGoals(long matchId, [FromBody] RegisterGoalsRequest request)
    {
        if (request.Goals == null || request.Goals.Count == 0)
            return BadRequest("No goals provided.");

        var match = await _db.Matches
            .Include(m => m.MatchPlayers)
            .FirstOrDefaultAsync(m => m.MatchId == matchId);

        if (match == null)
            return BadRequest("Match not found.");

        var mp = match.MatchPlayers;

        var realGoals = mp.ToDictionary(x => x.PlayerEntityId, x => x.Goals);
        var realAssists = mp.ToDictionary(x => x.PlayerEntityId, x => x.Assists);

        var reqGoals = new Dictionary<long, int>();
        var reqAssists = new Dictionary<long, int>();
        var reqPreAssists = new Dictionary<long, int>();

        foreach (var g in request.Goals)
        {
            if (!reqGoals.ContainsKey(g.ScorerPlayerEntityId))
                reqGoals[g.ScorerPlayerEntityId] = 0;

            reqGoals[g.ScorerPlayerEntityId]++;

            if (g.AssistPlayerEntityId.HasValue)
            {
                long id = g.AssistPlayerEntityId.Value;
                if (!reqAssists.ContainsKey(id))
                    reqAssists[id] = 0;

                reqAssists[id]++;
            }

            if (g.PreAssistPlayerEntityId.HasValue)
            {
                long id = g.PreAssistPlayerEntityId.Value;
                if (!reqPreAssists.ContainsKey(id))
                    reqPreAssists[id] = 0;

                reqPreAssists[id]++;
            }
        }

        foreach (var kv in reqGoals)
        {
            if (!realGoals.ContainsKey(kv.Key))
                return BadRequest($"Player {kv.Key} not found in match.");

            if (kv.Value > realGoals[kv.Key])
                return BadRequest($"Player {kv.Key} cannot receive {kv.Value} goals (max {realGoals[kv.Key]}).");
        }

        foreach (var kv in reqAssists)
        {
            if (!realAssists.ContainsKey(kv.Key))
                return BadRequest($"Player {kv.Key} not found in match.");

            if (kv.Value > realAssists[kv.Key])
                return BadRequest($"Player {kv.Key} cannot receive {kv.Value} assists (max {realAssists[kv.Key]}).");
        }

        long clubId = mp.First().ClubId;

        foreach (var g in request.Goals)
        {
            var entry = new MatchGoalLinkEntity
            {
                MatchId = matchId,
                ClubId = clubId,
                ScorerPlayerEntityId = g.ScorerPlayerEntityId,
                AssistPlayerEntityId = g.AssistPlayerEntityId,
                PreAssistPlayerEntityId = g.PreAssistPlayerEntityId
            };

            _db.MatchGoalLinks.Add(entry);

            if (g.PreAssistPlayerEntityId.HasValue)
            {
                var mpItem = mp.FirstOrDefault(x => x.PlayerEntityId == g.PreAssistPlayerEntityId.Value);
                if (mpItem != null)
                    mpItem.PreAssists++;
            }
        }

        await _db.SaveChangesAsync();

        return Ok(new { message = "Goals registered successfully." });
    }

    [HttpGet("{matchId}/goals")]
    public async Task<MatchGoalsResponseDto?> GetGoalsByMatchId(long matchId)
    {
        var match = await _db.Matches
            .Include(m => m.MatchPlayers)
            .FirstOrDefaultAsync(m => m.MatchId == matchId);

        if (match == null)
            return null;

        var matchPlayers = match.MatchPlayers;

        var goals = await _db.MatchGoalLinks
            .Where(g => g.MatchId == matchId)
            .ToListAsync();

        var dto = new MatchGoalsResponseDto
        {
            MatchId = matchId,
            TotalGoals = goals.Count,
            Goals = goals.Select(g =>
            {
                var scorer = matchPlayers.FirstOrDefault(mp => mp.PlayerEntityId == g.ScorerPlayerEntityId);
                var assist = g.AssistPlayerEntityId.HasValue
                    ? matchPlayers.FirstOrDefault(mp => mp.PlayerEntityId == g.AssistPlayerEntityId)
                    : null;

                var preAssist = g.PreAssistPlayerEntityId.HasValue
                    ? matchPlayers.FirstOrDefault(mp => mp.PlayerEntityId == g.PreAssistPlayerEntityId)
                    : null;

                return new MatchGoalItemDto
                {
                    MatchId = g.MatchId,
                    ClubId = g.ClubId,

                    ScorerPlayerEntityId = g.ScorerPlayerEntityId,
                    ScorerName = scorer?.ProName,

                    AssistPlayerEntityId = g.AssistPlayerEntityId,
                    AssistName = assist?.ProName,

                    PreAssistPlayerEntityId = g.PreAssistPlayerEntityId,
                    PreAssistName = preAssist?.ProName
                };
            }).ToList()
        };

        return dto;
    }
}
