using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

[ApiController]
[Route("api/[controller]")]
public class CalendarController : ControllerBase
{
    private readonly EAFCContext _context;

    public CalendarController(EAFCContext context)
    {
        _context = context;
    }

    // GET /api/Calendar?year=2025&month=9&clubId=123
    // GET /api/Calendar?year=2025&month=9&clubIds=1,2,3
    [HttpGet]
    public async Task<ActionResult<CalendarMonthDto>> GetMonthlyCalendar(
        [FromQuery] int year,
        [FromQuery] int month,
        [FromQuery] long? clubId = null,
        [FromQuery] string? clubIds = null)
    {
        if (year <= 0 || month < 1 || month > 12)
            return BadRequest("Parâmetros de data inválidos.");

        // Seleção: clubIds tem prioridade; senão, usa clubId único
        var selectedIds = ParseClubIds(clubIds);
        if (selectedIds.Count == 0)
        {
            if (clubId is null or <= 0) return BadRequest("Informe clubId ou clubIds.");
            selectedIds.Add(clubId!.Value);
        }
        var selected = selectedIds.ToHashSet();

        var startDate = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = startDate.AddMonths(1);

        // Partidas do mês envolvendo QUALQUER clube do conjunto
        var monthlyMatches = await _context.Matches
            .AsNoTracking()
            .Where(m => m.Timestamp >= startDate && m.Timestamp < endDate)
            .Where(m => m.Clubs.Any(c => selected.Contains(c.ClubId)))
            .Include(m => m.Clubs).ThenInclude(c => c.Details)
            .ToListAsync();

        // Agrupa por dia e sumariza do ponto de vista do CONJUNTO
        var dailySummaries = monthlyMatches
            .GroupBy(m => DateOnly.FromDateTime(m.Timestamp.Date))
            .Select(group => BuildDaySummary(group, selected))
            .Where(s => s.MatchesCount > 0)
            .OrderBy(s => s.Date)
            .ToList();

        return Ok(new CalendarMonthDto
        {
            Year = year,
            Month = month,
            Days = dailySummaries
        });
    }

    // GET /api/Calendar/day?date=2025-09-12&clubId=123
    // GET /api/Calendar/day?date=2025-09-12&clubIds=1,2,3
    [HttpGet("day")]
    public async Task<ActionResult<CalendarDayDetailsDto>> GetDayDetails(
        [FromQuery] DateOnly date,
        [FromQuery] long? clubId = null,
        [FromQuery] string? clubIds = null)
    {
        // Seleção
        var selectedIds = ParseClubIds(clubIds);
        if (selectedIds.Count == 0)
        {
            if (clubId is null or <= 0) return BadRequest("Informe clubId ou clubIds.");
            selectedIds.Add(clubId!.Value);
        }
        var selected = selectedIds.ToHashSet();

        var dayStart = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var dayEnd = dayStart.AddDays(1);

        var matchesOfDay = await _context.Matches
            .AsNoTracking()
            .Where(m => m.Timestamp >= dayStart && m.Timestamp < dayEnd)
            .Where(m => m.Clubs.Any(c => selected.Contains(c.ClubId)))
            .Include(m => m.Clubs).ThenInclude(c => c.Details)
            .Include(m => m.MatchPlayers).ThenInclude(mp => mp.Player)
            .OrderBy(m => m.Timestamp)
            .ToListAsync();

        // Monta lista de partidas (resultForClub relativo ao CONJUNTO)
        var list = matchesOfDay.Select(match => BuildMatchSummary(match, selected)).ToList();

        // Agrega W/D/L e GP/GC a partir da lista, do ponto de vista do CONJUNTO
        var details = new CalendarDayDetailsDto
        {
            Date = date,
            Matches = list
        };
        details.TotalMatches = list.Count;

        // W/E/D contam apenas quando exatamente um lado é do conjunto
        details.Wins = list.Count(m => m.ResultForClub == "W");
        details.Draws = list.Count(m => m.ResultForClub == "D");
        details.Losses = list.Count(m => m.ResultForClub == "L");

        // GP/GC contam apenas quando exatamente um lado é do conjunto
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

        return Ok(details);
    }

    #region Helpers

    private static List<long> ParseClubIds(string? csv)
    {
        var list = new List<long>();
        if (string.IsNullOrWhiteSpace(csv)) return list;

        foreach (var part in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (long.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) && id > 0)
                list.Add(id);
        }
        return list.Distinct().ToList();
    }

    // Resumo por DIA, do ponto de vista do CONJUNTO
    private CalendarDaySummaryDto BuildDaySummary(IGrouping<DateOnly, MatchEntity> matches, HashSet<long> selected)
    {
        int wins = 0, draws = 0, losses = 0, goalsFor = 0, goalsAgainst = 0;

        foreach (var match in matches)
        {
            var clubs = match.Clubs.OrderBy(c => c.Team).ToList();
            if (clubs.Count != 2) continue;

            var a = clubs[0];
            var b = clubs[1];
            bool aSel = selected.Contains(a.ClubId);
            bool bSel = selected.Contains(b.ClubId);

            // Se ambos pertencem ao conjunto, é "interno": não altera W/E/D nem GP/GC
            if (aSel && bSel) continue;

            // Exatamente um lado do conjunto
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

        return new CalendarDaySummaryDto
        {
            Date = matches.Key,
            MatchesCount = matches.Count(), // total de partidas que envolvem o conjunto
            Wins = wins,
            Draws = draws,
            Losses = losses,
            GoalsFor = goalsFor,
            GoalsAgainst = goalsAgainst
        };
    }

    // Resumo por PARTIDA, do ponto de vista do CONJUNTO
    private CalendarMatchSummaryDto BuildMatchSummary(MatchEntity match, HashSet<long> selected)
    {
        var clubs = match.Clubs.OrderBy(c => c.Team).ToList();
        if (clubs.Count != 2) throw new InvalidOperationException("Partida inválida, precisa de 2 clubes.");

        var a = clubs[0];
        var b = clubs[1];

        string resultForClub = "-";
        bool aSel = selected.Contains(a.ClubId);
        bool bSel = selected.Contains(b.ClubId);

        // Resultados só fazem sentido quando apenas um lado pertence ao conjunto
        if (aSel ^ bSel)
        {
            var mine = aSel ? a : b;
            var opp = aSel ? b : a;

            if (mine.Goals > opp.Goals) resultForClub = "W";
            else if (mine.Goals < opp.Goals) resultForClub = "L";
            else resultForClub = "D";
        }
        // se ambos/no one: resultForClub = "-"

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
