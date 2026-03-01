namespace EAFCMatchTracker.Application.Dtos;

public class ClubPlayoffAchievementDto
{
    public long ClubId { get; set; }
    public List<PlayoffAchievementDto> Achievements { get; set; } = new();
}
