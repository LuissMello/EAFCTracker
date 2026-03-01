namespace EAFCMatchTracker.Application.Dtos;

public class MatchGoalsResponseDto
{
    public long MatchId { get; set; }
    public int TotalGoals { get; set; }
    public List<MatchGoalItemDto> Goals { get; set; } = new();
}
