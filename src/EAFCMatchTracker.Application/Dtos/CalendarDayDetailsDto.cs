namespace EAFCMatchTracker.Application.Dtos;

public class CalendarDayDetailsDto
{
    public DateOnly Date { get; set; }
    public string TimeZoneId { get; set; } = "America/Sao_Paulo";
    public int TotalMatches { get; set; }
    public int Wins { get; set; }
    public int Draws { get; set; }
    public int Losses { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
    public List<CalendarMatchSummaryDto> Matches { get; set; } = new();
    public List<CalendarSessionDto> Sessions { get; set; } = new();
}

public class CalendarSessionDto
{
    public long Id { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime EndedAt { get; set; }
    public List<long> MatchIds { get; set; } = new();
}
