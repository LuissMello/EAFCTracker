using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/[controller]")]
public class CalendarController : ControllerBase
{
    private readonly EAFCContext _context;

    public CalendarController(EAFCContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<CalendarMonthDto>> GetMonthlyCalendar(
        [FromQuery] int year,
        [FromQuery] int month,
        [FromQuery] long clubId = 3463149)
    {
        if (year <= 0 || month < 1 || month > 12)
            return BadRequest("Parâmetros de data inválidos.");

        var startDate = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = startDate.AddMonths(1);

        var monthlyMatches = await _context.Matches
            .Where(m => m.Timestamp >= startDate && m.Timestamp < endDate)
            .Include(m => m.Clubs)
                .ThenInclude(c => c.Details)
            .ToListAsync();

        var dailySummaries = monthlyMatches
            .GroupBy(m => DateOnly.FromDateTime(m.Timestamp.Date))
            .Select(group => BuildDaySummary(group, clubId))
            .OrderBy(summary => summary.Date)
            .ToList();

        return Ok(new CalendarMonthDto
        {
            Year = year,
            Month = month,
            Days = dailySummaries
        });
    }

    [HttpGet("day")]
    public async Task<ActionResult<CalendarDayDetailsDto>> GetDayDetails(
        [FromQuery] DateOnly date,
        [FromQuery] long clubId = 3463149)
    {
        var dayStart = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var dayEnd = dayStart.AddDays(1);

        var matchesOfDay = await _context.Matches
            .Where(m => m.Timestamp >= dayStart && m.Timestamp < dayEnd)
            .Include(m => m.Clubs)
                .ThenInclude(c => c.Details)
            .Include(m => m.MatchPlayers)
                .ThenInclude(mp => mp.Player)
            .OrderBy(m => m.Timestamp)
            .ToListAsync();

        var daySummary = new CalendarDayDetailsDto
        {
            Date = date,
            Matches = matchesOfDay.Select(match => BuildMatchSummary(match, clubId)).ToList()
        };

        // agrega W/D/L e gols totais do ponto de vista do clube
        daySummary.TotalMatches = daySummary.Matches.Count;
        daySummary.Wins = daySummary.Matches.Count(m => m.ResultForClub == "W");
        daySummary.Draws = daySummary.Matches.Count(m => m.ResultForClub == "D");
        daySummary.Losses = daySummary.Matches.Count(m => m.ResultForClub == "L");
        daySummary.GoalsFor = daySummary.Matches.Sum(m =>
            m.ClubAId == clubId ? m.ClubAGoals : m.ClubBId == clubId ? m.ClubBGoals : 0);
        daySummary.GoalsAgainst = daySummary.Matches.Sum(m =>
            m.ClubAId == clubId ? m.ClubBGoals : m.ClubBId == clubId ? m.ClubAGoals : 0);

        return Ok(daySummary);
    }

    #region Helpers

    private CalendarDaySummaryDto BuildDaySummary(IGrouping<DateOnly, MatchEntity> matches, long clubId)
    {
        int wins = 0, draws = 0, losses = 0, goalsFor = 0, goalsAgainst = 0;

        foreach (var match in matches)
        {
            var clubs = match.Clubs.OrderBy(c => c.Team).ToList();
            if (clubs.Count != 2) continue;

            var currentClub = clubs.FirstOrDefault(c => c.ClubId == clubId);
            var opponentClub = clubs.FirstOrDefault(c => c.ClubId != clubId);

            if (currentClub == null || opponentClub == null) continue;

            goalsFor += currentClub.Goals;
            goalsAgainst += opponentClub.Goals;

            if (currentClub.Goals > opponentClub.Goals) wins++;
            else if (currentClub.Goals < opponentClub.Goals) losses++;
            else draws++;
        }

        return new CalendarDaySummaryDto
        {
            Date = matches.Key,
            MatchesCount = matches.Count(),
            Wins = wins,
            Draws = draws,
            Losses = losses,
            GoalsFor = goalsFor,
            GoalsAgainst = goalsAgainst
        };
    }

    private CalendarMatchSummaryDto BuildMatchSummary(MatchEntity match, long clubId)
    {
        var clubs = match.Clubs.OrderBy(c => c.Team).ToList();
        if (clubs.Count != 2) throw new InvalidOperationException("Partida inválida, precisa de 2 clubes.");

        var clubA = clubs[0];
        var clubB = clubs[1];

        var resultForClub = "-";
        if (clubA.ClubId == clubId || clubB.ClubId == clubId)
        {
            var currentClub = clubA.ClubId == clubId ? clubA : clubB;
            var opponentClub = currentClub == clubA ? clubB : clubA;

            if (currentClub.Goals > opponentClub.Goals) resultForClub = "W";
            else if (currentClub.Goals < opponentClub.Goals) resultForClub = "L";
            else resultForClub = "D";
        }

        var aggregatedStats = AggregateMatchStats(match.MatchPlayers);

        return new CalendarMatchSummaryDto
        {
            MatchId = match.MatchId,
            Timestamp = match.Timestamp,
            ClubAId = clubA.ClubId,
            ClubAName = clubA.Details?.Name ?? $"Clube {clubA.ClubId}",
            ClubAGoals = clubA.Goals,
            ClubACrestAssetId = clubA.Details?.CrestAssetId,
            ClubBId = clubB.ClubId,
            ClubBName = clubB.Details?.Name ?? $"Clube {clubB.ClubId}",
            ClubBGoals = clubB.Goals,
            ClubBCrestAssetId = clubB.Details?.CrestAssetId,
            ResultForClub = resultForClub,
            Stats = aggregatedStats
        };
    }

    private CalendarMatchStatLineDto AggregateMatchStats(IEnumerable<MatchPlayerEntity> players)
    {
        int goals = players.Sum(p => p.Goals);
        int assists = players.Sum(p => p.Assists);
        int shots = players.Sum(p => p.Shots);
        int passesMade = players.Sum(p => p.Passesmade);
        int passAttempts = players.Sum(p => p.Passattempts);
        int tacklesMade = players.Sum(p => p.Tacklesmade);
        int tackleAttempts = players.Sum(p => p.Tackleattempts);
        int redCards = players.Sum(p => p.Redcards);
        int saves = players.Sum(p => p.Saves);
        int moms = players.Count(p => p.Mom);
        double avgRating = players.Any() ? players.Average(p => p.Rating) : 0;

        return new CalendarMatchStatLineDto
        {
            TotalGoals = goals,
            TotalAssists = assists,
            TotalShots = shots,
            TotalPassesMade = passesMade,
            TotalPassAttempts = passAttempts,
            TotalTacklesMade = tacklesMade,
            TotalTackleAttempts = tackleAttempts,
            TotalRedCards = redCards,
            TotalSaves = saves,
            TotalMom = moms,
            AvgRating = avgRating,
            PassAccuracyPercent = passAttempts > 0 ? (passesMade * 100.0) / passAttempts : 0,
            TackleSuccessPercent = tackleAttempts > 0 ? (tacklesMade * 100.0) / tackleAttempts : 0,
            GoalAccuracyPercent = shots > 0 ? (goals * 100.0) / shots : 0
        };
    }

    #endregion
}
