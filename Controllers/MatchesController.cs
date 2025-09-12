using EAFCMatchTracker.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EAFCMatchTracker.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MatchesController : ControllerBase
{
    private readonly EAFCContext _db;

    public MatchesController(EAFCContext dbContext) => _db = dbContext;

    [HttpGet]
    public async Task<ActionResult<List<MatchDto>>> GetAllMatches(CancellationToken ct)
    {
        var matches = await _db.Matches
            .AsNoTracking()
            .OrderByDescending(m => m.Timestamp)
            .ToMatchDtoListAsync(ct);
        return Ok(matches);
    }

    [HttpGet("{matchId:long}")]
    public async Task<ActionResult<MatchDto>> GetMatchById(long matchId, CancellationToken ct)
    {
        var dto = await _db.Matches
            .AsNoTracking()
            .Where(m => m.MatchId == matchId)
            .FirstMatchDtoOrDefaultAsync(ct);

        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpGet("{matchId:long}/statistics")]
    public async Task<IActionResult> GetMatchStatisticsById(long matchId, CancellationToken ct)
    {
        var match = await _db.Matches
            .AsNoTracking()
            .Include(m => m.Clubs).ThenInclude(c => c.Details)
            .Include(m => m.MatchPlayers).ThenInclude(mp => mp.Player)
            .FirstOrDefaultAsync(m => m.MatchId == matchId, ct);

        if (match == null) return NotFound();

        var overall = StatsAggregator.BuildOverallForSingleMatch(match.MatchPlayers);
        var playersStats = StatsAggregator.BuildPerPlayer(match.MatchPlayers);
        var clubsStats = StatsAggregator.BuildPerClub(match.MatchPlayers, match.Clubs.ToDictionary(c => c.ClubId));

        var clubIds = match.Clubs.Select(c => c.ClubId).Distinct().ToList();
        var overallEntities = await _db.OverallStats
            .AsNoTracking()
            .Where(o => clubIds.Contains(o.ClubId))
            .ToListAsync(ct);

        var clubsOverall = StatsAggregator.BuildClubsOverall(overallEntities);

        return Ok(new
        {
            Overall = overall,
            Players = playersStats,
            Clubs = clubsStats,
            ClubsOverall = clubsOverall
        });
    }

    [HttpGet("{matchId:long}/players/{playerId:long}/statistics")]
    public async Task<ActionResult<MatchPlayerStatsDto>> GetPlayerStatisticsByMatchAndPlayer(long matchId, long playerId, CancellationToken ct)
    {
        var dto = await _db.MatchPlayers
            .AsNoTracking()
            .Where(mp => mp.MatchId == matchId && mp.PlayerEntityId == playerId)
            .ProjectPlayerStats()
            .FirstOrDefaultAsync(ct);

        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpDelete("{matchId:long}")]
    public async Task<IActionResult> DeleteMatch(long matchId, CancellationToken ct)
    {
        var match = await _db.Matches
            .Include(m => m.MatchPlayers)
            .Include(m => m.Clubs)
            .FirstOrDefaultAsync(m => m.MatchId == matchId, ct);

        if (match == null) return NotFound(new { message = "Partida não encontrada" });

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

        return NoContent();
    }
}
