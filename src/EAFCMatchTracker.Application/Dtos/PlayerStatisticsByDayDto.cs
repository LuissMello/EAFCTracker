namespace EAFCMatchTracker.Application.Dtos;

public class PlayerStatisticsByDayDto
{
    public DateOnly Date { get; set; }

    public List<PlayerStatisticsDto> Statistics { get; set; } = new();
}
