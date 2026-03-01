namespace EAFCMatchTracker.Application.Dtos;

public class MatchStatisticsResponseDto
{
    public MatchStatisticsDto Overall { get; set; } = default!;
    public List<PlayerStatisticsDto> Players { get; set; } = new();
    public List<ClubStatisticsDto> Clubs { get; set; } = new();
}
