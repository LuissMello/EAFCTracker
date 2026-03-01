namespace EAFCMatchTracker.Application.Dtos;

public class PlayoffAchievementDto
{
    public string SeasonId { get; set; } = "";
    public string? SeasonName { get; set; }
    public string? BestDivision { get; set; }
    public string? BestFinishGroup { get; set; }
    public DateTime RetrievedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
