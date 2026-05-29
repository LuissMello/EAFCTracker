using EAFCMatchTracker.Application.Dtos;
using EAFCMatchTracker.Application.Interfaces.Repositories;
using EAFCMatchTracker.Application.Interfaces.Services;
using EAFCMatchTracker.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EAFCMatchTracker.Application.Services;

public class TrendsService : ITrendsService
{
    private readonly IMatchRepository _matchRepository;
    private readonly EAFCContext _db;
    private readonly ILogger<TrendsService> _logger;

    public TrendsService(IMatchRepository matchRepository, EAFCContext db, ILogger<TrendsService> logger)
    {
        _matchRepository = matchRepository;
        _db = db;
        _logger = logger;
    }

    public async Task<object> GetClubTrendsAsync(long clubId, int last, DateTime? since, DateTime? until, CancellationToken ct)
    {
        _logger.LogInformation("TrendsService.GetClubTrendsAsync ClubId={ClubId} last={Last}", clubId, last);

        var matches = await _matchRepository.GetMatchesForTrendsAsync(clubId, last, since, until, ct);

        if (!matches.Any())
            return new ClubTrendsDto { ClubId = clubId, ClubName = $"Clube {clubId}" };

        var points = new List<MatchTrendPointDto>();
        string clubName = $"Clube {clubId}";

        foreach (var m in matches.OrderBy(x => x.Timestamp))
        {
            var mcThis = m.Clubs.FirstOrDefault(c => c.ClubId == clubId);
            var mcOpp = m.Clubs.FirstOrDefault(c => c.ClubId != clubId);

            if (mcThis == null || mcOpp == null) continue;

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

        return new ClubTrendsDto
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
    }

    public async Task<object> GetTopScorersAsync(long clubId, DateTime? since, DateTime? until, int limit, CancellationToken ct)
    {
        _logger.LogInformation("TrendsService.GetTopScorersAsync ClubId={ClubId}", clubId);

        var q = _db.MatchPlayers
            .Include(mp => mp.Match)
            .Include(mp => mp.Player)
            .Where(mp => mp.Player.ClubId == clubId);

        if (since.HasValue) q = q.Where(mp => mp.Match.Timestamp >= since.Value);
        if (until.HasValue) q = q.Where(mp => mp.Match.Timestamp <= until.Value);

        return await q
            .GroupBy(mp => mp.PlayerEntityId)
            .Select(g => new TopScorerItemDto
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
            .ToListAsync(ct);
    }

    private static List<double> MovAvg(List<double> src, int window)
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
}
