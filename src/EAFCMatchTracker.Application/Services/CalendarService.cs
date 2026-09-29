using EAFCMatchTracker.Application.Dtos;
using EAFCMatchTracker.Application.Interfaces.Services;
using EAFCMatchTracker.Application.Time;
using EAFCMatchTracker.Domain.Entities;
using EAFCMatchTracker.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EAFCMatchTracker.Application.Services;

public class CalendarService : ICalendarService
{
    private readonly EAFCContext _db;
    private readonly ILogger<CalendarService> _logger;
    private readonly ClubSessionService _sessionService;

    public CalendarService(EAFCContext db, ILogger<CalendarService> logger, ClubSessionService sessionService)
    {
        _db = db;
        _logger = logger;
        _sessionService = sessionService;
    }

    public async Task<CalendarMonthDto> GetMonthlyCalendarAsync(int year, int month, HashSet<long> selected, bool sessions, CancellationToken ct)
    {
        _logger.LogInformation("CalendarService.GetMonthlyCalendarAsync year={Year} month={Month}", year, month);

        // Mês do calendário de Brasília -> limites UTC
        var firstDay = new DateOnly(year, month, 1);
        var startDate = BrazilTime.StartOfLocalDayUtc(firstDay);
        var endDate = BrazilTime.StartOfLocalDayUtc(firstDay.AddMonths(1));

        var sessionList = sessions
            ? (await _sessionService.GetForClubAsync(selected.Single(), ct))
                .Where(s => s.Date.Year == year && s.Date.Month == month).ToList()
            : new List<ClubSession>();
        var sessionMatchIds = sessionList.SelectMany(s => s.MatchIds).ToHashSet();
        var monthlyQuery = _db.Matches
            .AsNoTracking()
            .Where(m => m.Clubs.Any(c => selected.Contains(c.ClubId)));
        monthlyQuery = sessions
            ? monthlyQuery.Where(m => sessionMatchIds.Contains(m.MatchId))
            : monthlyQuery.Where(m => m.Timestamp >= startDate && m.Timestamp < endDate);
        var monthlyMatches = await monthlyQuery
            .Include(m => m.Clubs).ThenInclude(c => c.Details)
            .ToListAsync(ct);

        var sessionDates = sessionList.SelectMany(s => s.MatchIds.Select(id => new { id, s.Date }))
            .ToDictionary(x => x.id, x => x.Date);
        var dailySummaries = monthlyMatches
            .GroupBy(m => sessions ? sessionDates[m.MatchId] : BrazilTime.ToLocalDate(m.Timestamp))
            .Select(group =>
            {
                try { return BuildDaySummary(group, selected); }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error building day summary for date {Date}", group.Key);
                    return null;
                }
            })
            .Where(s => s != null && s.MatchesCount > 0)
            .OrderBy(s => s!.Date)
            .ToList();

        return new CalendarMonthDto
        {
            Year = year,
            Month = month,
            Days = dailySummaries!
        };
    }

    public async Task<CalendarDayDetailsDto> GetDayDetailsAsync(DateOnly date, HashSet<long> selected, bool sessions, CancellationToken ct)
    {
        _logger.LogInformation("CalendarService.GetDayDetailsAsync date={Date}", date);

        // Dia do calendário de Brasília -> [00:00, 24:00) em UTC
        var dayStart = BrazilTime.StartOfLocalDayUtc(date);
        var dayEnd = BrazilTime.StartOfLocalDayUtc(date.AddDays(1));

        var sessionList = sessions
            ? (await _sessionService.GetForClubAsync(selected.Single(), ct)).Where(s => s.Date == date).ToList()
            : new List<ClubSession>();
        var sessionMatchIds = sessionList.SelectMany(s => s.MatchIds).ToHashSet();
        var dayQuery = _db.Matches
            .AsNoTracking()
            .Where(m => m.Clubs.Any(c => selected.Contains(c.ClubId)));
        dayQuery = sessions
            ? dayQuery.Where(m => sessionMatchIds.Contains(m.MatchId))
            : dayQuery.Where(m => m.Timestamp >= dayStart && m.Timestamp < dayEnd);
        var matchesOfDay = await dayQuery
            .Include(m => m.Clubs).ThenInclude(c => c.Details)
            .Include(m => m.MatchPlayers).ThenInclude(mp => mp.Player)
            .OrderBy(m => m.Timestamp).ThenBy(m => m.MatchId)
            .ToListAsync(ct);

        var list = new List<CalendarMatchSummaryDto>();
        foreach (var match in matchesOfDay)
        {
            try { list.Add(BuildMatchSummary(match, selected)); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error building match summary for matchId={MatchId}", match.MatchId);
            }
        }

        var details = new CalendarDayDetailsDto
        {
            Date = date,
            TimeZoneId = sessions
                ? (await _db.TrackedClubs.AsNoTracking().Where(c => c.ClubId == selected.Single())
                    .Select(c => c.TimeZoneId).FirstOrDefaultAsync(ct) ?? "America/Sao_Paulo")
                : "America/Sao_Paulo",
            Matches = list,
            Sessions = sessionList.Select(s => new CalendarSessionDto
            {
                Id = s.Id, StartedAt = s.StartedAt, EndedAt = s.EndedAt, MatchIds = s.MatchIds
            }).ToList()
        };
        details.TotalMatches = list.Count;
        details.Wins = list.Count(m => m.ResultForClub == "W");
        details.Draws = list.Count(m => m.ResultForClub == "D");
        details.Losses = list.Count(m => m.ResultForClub == "L");

        int gf = 0, ga = 0;
        foreach (var m in list)
        {
            bool aSel = selected.Contains(m.ClubAId);
            bool bSel = selected.Contains(m.ClubBId);
            if (aSel && !bSel) { gf += m.ClubAGoals; ga += m.ClubBGoals; }
            else if (bSel && !aSel) { gf += m.ClubBGoals; ga += m.ClubAGoals; }
        }
        details.GoalsFor = gf;
        details.GoalsAgainst = ga;

        return details;
    }

    private CalendarDaySummaryDto BuildDaySummary(IGrouping<DateOnly, MatchEntity> matches, HashSet<long> selected)
    {
        int wins = 0, draws = 0, losses = 0, goalsFor = 0, goalsAgainst = 0;

        foreach (var match in matches)
        {
            try
            {
                var clubs = match.Clubs.OrderBy(c => c.Team).ToList();
                if (clubs.Count != 2) continue;

                var a = clubs[0];
                var b = clubs[1];
                bool aSel = selected.Contains(a.ClubId);
                bool bSel = selected.Contains(b.ClubId);

                if (aSel && bSel) continue;

                if (aSel || bSel)
                {
                    var mine = aSel ? a : b;
                    var opp = aSel ? b : a;

                    goalsFor += mine.Goals;
                    goalsAgainst += opp.Goals;

                    if (mine.Goals > opp.Goals) wins++;
                    else if (mine.Goals < opp.Goals) losses++;
                    else draws++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing matchId={MatchId} in BuildDaySummary", match.MatchId);
            }
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

    private CalendarMatchSummaryDto BuildMatchSummary(MatchEntity match, HashSet<long> selected)
    {
        var clubs = match.Clubs.OrderBy(c => c.Team).ToList();
        if (clubs.Count != 2) throw new InvalidOperationException("Partida inválida, precisa de 2 clubes.");

        var a = clubs[0];
        var b = clubs[1];

        string resultForClub = "-";
        bool aSel = selected.Contains(a.ClubId);
        bool bSel = selected.Contains(b.ClubId);

        if (aSel ^ bSel)
        {
            var mine = aSel ? a : b;
            var opp = aSel ? b : a;

            if (mine.Goals > opp.Goals) resultForClub = "W";
            else if (mine.Goals < opp.Goals) resultForClub = "L";
            else resultForClub = "D";
        }

        var aggregatedStats = AggregateMatchStats(match.MatchPlayers);

        return new CalendarMatchSummaryDto
        {
            MatchId = match.MatchId,
            Timestamp = match.Timestamp,
            ClubAId = a.ClubId,
            ClubAName = a.Details?.Name ?? $"Clube {a.ClubId}",
            ClubAGoals = a.Goals,
            ClubACrestAssetId = a.Team.ToString(),
            ClubBId = b.ClubId,
            ClubBName = b.Details?.Name ?? $"Clube {b.ClubId}",
            ClubBGoals = b.Goals,
            ClubBCrestAssetId = b.Team.ToString(),
            ResultForClub = resultForClub,
            Stats = aggregatedStats
        };
    }

    private static CalendarMatchStatLineDto AggregateMatchStats(IEnumerable<MatchPlayerEntity> players)
    {
        var list = players.ToList();
        int goals = list.Sum(p => p.Goals);
        int assists = list.Sum(p => p.Assists);
        int preAssists = list.Sum(p => p.PreAssists);
        int shots = list.Sum(p => p.Shots);
        int passesMade = list.Sum(p => p.Passesmade);
        int passAttempts = list.Sum(p => p.Passattempts);
        int tacklesMade = list.Sum(p => p.Tacklesmade);
        int tackleAttempts = list.Sum(p => p.Tackleattempts);
        int redCards = list.Sum(p => p.Redcards);
        int saves = list.Sum(p => p.Saves);
        int moms = list.Count(p => p.Mom);
        double avgRating = list.Any() ? list.Average(p => p.Rating) : 0;

        return new CalendarMatchStatLineDto
        {
            TotalGoals = goals,
            TotalAssists = assists,
            TotalPreAssists = preAssists,
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
}
