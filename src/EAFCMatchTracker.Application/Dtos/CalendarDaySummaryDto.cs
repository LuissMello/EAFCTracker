namespace EAFCMatchTracker.Application.Dtos;

public class CalendarDaySummaryDto
{
    public DateOnly Date { get; set; }
    public int MatchesCount { get; set; }
    public int Wins { get; set; }
    public int Draws { get; set; }
    public int Losses { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
}
