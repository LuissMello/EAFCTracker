namespace EAFCMatchTracker.Application.Dtos;

public class FullMatchStatisticsDto
{
    public MatchStatisticsDto Overall { get; set; }
    public List<PlayerStatisticsDto> Players { get; set; }
    public List<ClubStatisticsDto> Clubs { get; set; }
}
