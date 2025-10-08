using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/[controller]")]
public class TrendsController : ControllerBase
{
    private readonly EAFCContext _db;
    private readonly ILogger<TrendsController> _logger;

    public TrendsController(EAFCContext db, ILogger<TrendsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    public class MatchTrendPointDto
    {
        public long MatchId { get; set; }
        public DateTime Timestamp { get; set; }
        public long OpponentClubId { get; set; }
        public string OpponentName { get; set; } = "";
        public int GoalsFor { get; set; }
        public int GoalsAgainst { get; set; }
        public string Result { get; set; } = ""; // "W" | "D" | "L"
        public int Shots { get; set; }
        public int PassesMade { get; set; }
        public int PassAttempts { get; set; }
        public double PassAccuracyPercent { get; set; }
        public int TacklesMade { get; set; }
        public int TackleAttempts { get; set; }
        public double TackleSuccessPercent { get; set; }
        public double AvgRating { get; set; }
        public bool MomOccurred { get; set; }
    }

    public class ClubTrendsDto
    {
        public long ClubId { get; set; }
        public string ClubName { get; set; } = "";
        public List<MatchTrendPointDto> Series { get; set; } = new();

        // Forma (últimos 5 e 10)
        public string FormLast5 { get; set; } = "";  // ex: "W W D L W"
        public string FormLast10 { get; set; } = "";

        // Streaks
        public int CurrentUnbeaten { get; set; }     // sem perder (W/D)
        public int CurrentWins { get; set; }         // vitórias seguidas
        public int CurrentCleanSheets { get; set; }  // jogos seguidos sem sofrer gol

        // Médias móveis (sobre a série, calculadas no controller)
        public List<double> MovingAvgPassAcc_5 { get; set; } = new();
        public List<double> MovingAvgRating_5 { get; set; } = new();
        public List<double> MovingAvgTackleAcc_5 { get; set; } = new();
    }

    public class TopItemDto
    {
        public long PlayerEntityId { get; set; }
        public long PlayerId { get; set; }
        public string PlayerName { get; set; } = "";
        public long ClubId { get; set; }
        public int Goals { get; set; }
        public int Assists { get; set; }
        public int Matches { get; set; }
        public double AvgRating { get; set; }
        public int Mom { get; set; }
    }

    [HttpGet("club/{clubId:long}")]
    public async Task<IActionResult> GetClubTrends(
        long clubId,
        [FromQuery] int last = 30,
        [FromQuery] DateTime? since = null,
        [FromQuery] DateTime? until = null)
    {
        _logger.LogInformation("GetClubTrends called for ClubId={ClubId}, last={Last}, since={Since}, until={Until}", clubId, last, since, until);

        try
        {
            var matches = await _db.Matches
                .Include(m => m.Clubs)
                    .ThenInclude(c => c.Details)
                .Include(m => m.MatchPlayers)
                    .ThenInclude(mp => mp.Player)
                .Where(m => m.Clubs.Any(c => c.ClubId == clubId))
                .Where(m => !since.HasValue || m.Timestamp >= since.Value)
                .Where(m => !until.HasValue || m.Timestamp <= until.Value)
                .OrderByDescending(m => m.Timestamp)
                .Take(last > 0 ? last : 30)
                .ToListAsync();

            if (!matches.Any())
            {
                _logger.LogWarning("No matches found for ClubId={ClubId}", clubId);
                return Ok(new ClubTrendsDto { ClubId = clubId, ClubName = $"Clube {clubId}" });
            }

            var points = new List<MatchTrendPointDto>();
            string clubName = $"Clube {clubId}";

            foreach (var m in matches.OrderBy(x => x.Timestamp))
            {
                var mcThis = m.Clubs.FirstOrDefault(c => c.ClubId == clubId);
                var mcOpp = m.Clubs.FirstOrDefault(c => c.ClubId != clubId);

                if (mcThis == null || mcOpp == null)
                {
                    _logger.LogWarning("MatchClubEntity not found for ClubId={ClubId} in MatchId={MatchId}", clubId, m.MatchId);
                    continue;
                }

                clubName = mcThis.Details?.Name ?? clubName;
                var opponentName = mcOpp.Details?.Name ?? $"Clube {mcOpp.ClubId}";

                var rows = m.MatchPlayers.Where(p => p.ClubId == clubId).ToList();

                var passesMade = rows.Sum(x => x.Passesmade);
                var passAttempts = rows.Sum(x => x.Passattempts);
                var tacklesMade = rows.Sum(x => x.Tacklesmade);
                var tackleAttempts = rows.Sum(x => x.Tackleattempts);
                var shots = rows.Sum(x => x.Shots);
                var avgRating = rows.Any() ? rows.Average(x => x.Rating) : 0;

                var result = mcThis.Goals > mcOpp.Goals ? "W"
                           : mcThis.Goals < mcOpp.Goals ? "L" : "D";

                points.Add(new MatchTrendPointDto
                {
                    MatchId = m.MatchId,
                    Timestamp = m.Timestamp,
                    OpponentClubId = mcOpp.ClubId,
                    OpponentName = opponentName,
                    GoalsFor = mcThis.Goals,
                    GoalsAgainst = mcOpp.Goals,
                    Result = result,
                    Shots = shots,
                    PassesMade = passesMade,
                    PassAttempts = passAttempts,
                    PassAccuracyPercent = passAttempts > 0 ? (passesMade * 100.0) / passAttempts : 0,
                    TacklesMade = tacklesMade,
                    TackleAttempts = tackleAttempts,
                    TackleSuccessPercent = tackleAttempts > 0 ? (tacklesMade * 100.0) / tackleAttempts : 0,
                    AvgRating = avgRating,
                    MomOccurred = rows.Any(r => r.Mom)
                });
            }

            int currentUnbeaten = 0, currentWins = 0, currentCleanSheets = 0;
            foreach (var p in points.AsEnumerable().Reverse())
            {
                if (p.Result == "W" || p.Result == "D") currentUnbeaten++; else break;
            }
            foreach (var p in points.AsEnumerable().Reverse())
            {
                if (p.Result == "W") currentWins++; else break;
            }
            foreach (var p in points.AsEnumerable().Reverse())
            {
                if (p.GoalsAgainst == 0) currentCleanSheets++; else break;
            }

            string form5 = string.Join(' ', points.AsEnumerable().Reverse().Take(5).Select(p => p.Result));
            string form10 = string.Join(' ', points.AsEnumerable().Reverse().Take(10).Select(p => p.Result));

            static List<double> MovAvg(List<double> src, int window)
            {
                var outp = new List<double>(src.Count);
                double sum = 0;
                var q = new Queue<double>();
                foreach (var v in src)
                {
                    q.Enqueue(v); sum += v;
                    if (q.Count > window) sum -= q.Dequeue();
                    outp.Add(sum / q.Count);
                }
                return outp;
            }

            var dto = new ClubTrendsDto
            {
                ClubId = clubId,
                ClubName = clubName,
                Series = points,
                FormLast5 = form5,
                FormLast10 = form10,
                CurrentUnbeaten = currentUnbeaten,
                CurrentWins = currentWins,
                CurrentCleanSheets = currentCleanSheets,
                MovingAvgPassAcc_5 = MovAvg(points.Select(p => p.PassAccuracyPercent).ToList(), 5),
                MovingAvgRating_5 = MovAvg(points.Select(p => p.AvgRating).ToList(), 5),
                MovingAvgTackleAcc_5 = MovAvg(points.Select(p => p.TackleSuccessPercent).ToList(), 5),
            };

            _logger.LogInformation("Club trends calculated for ClubId={ClubId}", clubId);
            return Ok(dto);
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
        [FromQuery] int limit = 10)
    {
        _logger.LogInformation("GetTopScorers called for ClubId={ClubId}, since={Since}, until={Until}, limit={Limit}", clubId, since, until, limit);

        try
        {
            var q = _db.MatchPlayers
                .Include(mp => mp.Match)
                .Include(mp => mp.Player)
                .Where(mp => mp.Player.ClubId == clubId);

            if (since.HasValue) q = q.Where(mp => mp.Match.Timestamp >= since.Value);
            if (until.HasValue) q = q.Where(mp => mp.Match.Timestamp <= until.Value);

            var grouped = await q
                .GroupBy(mp => mp.PlayerEntityId)
                .Select(g => new TopItemDto
                {
                    PlayerEntityId = g.Key,
                    PlayerId = g.Max(x => x.Player.PlayerId),
                    PlayerName = g.Max(x => x.Player.Playername),
                    ClubId = g.Max(x => x.Player.ClubId),
                    Goals = g.Sum(x => x.Goals),
                    Assists = g.Sum(x => x.Assists),
                    Matches = g.Count(),
                    AvgRating = g.Average(x => x.Rating),
                    Mom = g.Count(x => x.Mom),
                })
                .OrderByDescending(x => x.Goals)
                .ThenByDescending(x => x.Assists)
                .ThenByDescending(x => x.AvgRating)
                .Take(limit > 0 ? limit : 10)
                .ToListAsync();

            _logger.LogInformation("Top scorers calculated for ClubId={ClubId}", clubId);
            return Ok(grouped);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetTopScorers for ClubId={ClubId}", clubId);
            return StatusCode(500, "An error occurred while processing your request.");
        }
    }
}
