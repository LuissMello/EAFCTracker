using EAFCMatchTracker.Application.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EAFCMatchTracker.Api.Controllers;

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

    [HttpGet("{playerEntityId:long}/profile")]
    public async Task<ActionResult<PlayerProfileDto>> GetPlayerProfile(long playerEntityId, CancellationToken ct)
    {
        _logger.LogInformation("GetPlayerProfile called for playerEntityId={PlayerEntityId}", playerEntityId);
        try
        {
            var matchPlayers = await _db.MatchPlayers
                .AsNoTracking()
                .Include(mp => mp.Player)
                .Include(mp => mp.Match).ThenInclude(m => m.Clubs).ThenInclude(c => c.Details)
                .Where(mp => mp.PlayerEntityId == playerEntityId)
                .OrderBy(mp => mp.Match.Timestamp)
                .ToListAsync(ct);

            if (matchPlayers.Count == 0)
                return NotFound();

            var first = matchPlayers.First();
            var playerEntity = first.Player;

            var proName = matchPlayers
                .OrderByDescending(mp => mp.Match.Timestamp)
                .Select(mp => mp.ProName)
                .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));

            var name = !string.IsNullOrWhiteSpace(proName) ? proName : playerEntity?.Playername ?? "";
            var accountName = playerEntity?.Playername ?? "";

            var history = new List<PlayerMatchHistoryDto>();

            double bestRating = double.MinValue;
            double worstRating = double.MaxValue;
            long? bestRatingMatchId = null;
            long? worstRatingMatchId = null;

            foreach (var mp in matchPlayers)
            {
                var match = mp.Match;
                var ourClub = match.Clubs.FirstOrDefault(c => c.ClubId == mp.ClubId);
                var oppClub = match.Clubs.FirstOrDefault(c => c.ClubId != mp.ClubId);

                int gf = ourClub?.Goals ?? 0;
                int ga = oppClub?.Goals ?? 0;

                string result;
                if (gf > ga) result = "W";
                else if (gf < ga) result = "L";
                else result = "D";

                history.Add(new PlayerMatchHistoryDto
                {
                    MatchId = match.MatchId,
                    Timestamp = match.Timestamp,
                    Goals = mp.Goals,
                    Assists = mp.Assists,
                    PreAssists = mp.PreAssists,
                    Rating = mp.Rating,
                    Pos = mp.Pos ?? "",
                    Mom = mp.Mom,
                    SecondsPlayed = mp.SecondsPlayed,
                    Result = result,
                    GoalsFor = gf,
                    GoalsAgainst = ga,
                    OpponentName = oppClub?.Details?.Name
                });

                if (mp.Rating > bestRating)
                {
                    bestRating = mp.Rating;
                    bestRatingMatchId = match.MatchId;
                }
                if (mp.Rating < worstRating)
                {
                    worstRating = mp.Rating;
                    worstRatingMatchId = match.MatchId;
                }
            }

            history = history.OrderByDescending(h => h.Timestamp).ToList();

            int totalWins = history.Count(h => h.Result == "W");
            int totalDraws = history.Count(h => h.Result == "D");
            int totalLosses = history.Count(h => h.Result == "L");

            var positions = matchPlayers
                .GroupBy(mp => mp.Pos ?? "")
                .ToDictionary(g => g.Key, g => g.Count());

            var proOverall = matchPlayers
                .OrderByDescending(mp => mp.Match.Timestamp)
                .Select(mp => mp.ProOverall)
                .FirstOrDefault(v => v.HasValue);

            var dto = new PlayerProfileDto
            {
                PlayerEntityId = playerEntityId,
                Name = name,
                AccountName = accountName,
                PlayerId = playerEntity?.PlayerId ?? 0,
                ClubId = playerEntity?.ClubId ?? 0,
                TotalMatches = matchPlayers.Count,
                TotalWins = totalWins,
                TotalDraws = totalDraws,
                TotalLosses = totalLosses,
                TotalGoals = matchPlayers.Sum(mp => (int)mp.Goals),
                TotalAssists = matchPlayers.Sum(mp => (int)mp.Assists),
                TotalPreAssists = matchPlayers.Sum(mp => (int)mp.PreAssists),
                AvgRating = matchPlayers.Count > 0 ? matchPlayers.Average(mp => mp.Rating) : 0,
                TotalMoM = matchPlayers.Count(mp => mp.Mom),
                TotalRedCards = matchPlayers.Sum(mp => (int)mp.Redcards),
                TotalCleanSheets = matchPlayers.Sum(mp => (int)mp.Cleansheetsany),
                TotalSaves = matchPlayers.Sum(mp => (int)mp.Saves),
                HatTricks = matchPlayers.Count(mp => mp.Goals >= 3),
                BestRating = bestRating == double.MinValue ? 0 : bestRating,
                WorstRating = worstRating == double.MaxValue ? 0 : worstRating,
                BestRatingMatchId = bestRatingMatchId,
                WorstRatingMatchId = worstRatingMatchId,
                MostGoalsInMatch = matchPlayers.Max(mp => (int)mp.Goals),
                MostAssistsInMatch = matchPlayers.Max(mp => (int)mp.Assists),
                ProOverall = proOverall,
                Positions = positions,
                History = history
            };

            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetPlayerProfile for playerEntityId={PlayerEntityId}", playerEntityId);
            return StatusCode(500, "Erro interno ao buscar perfil do jogador.");
        }
    }
}
