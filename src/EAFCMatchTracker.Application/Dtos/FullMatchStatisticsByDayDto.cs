namespace EAFCMatchTracker.Application.Dtos;

public sealed class FullMatchStatisticsByDayDto
{
    public DateOnly Date { get; set; }
    public FullMatchStatisticsDto Statistics { get; set; } = new();
}
