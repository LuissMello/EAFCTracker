namespace EAFCMatchTracker.Application.Dtos;

public class CalendarMonthDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public List<CalendarDaySummaryDto> Days { get; set; } = new();
}
