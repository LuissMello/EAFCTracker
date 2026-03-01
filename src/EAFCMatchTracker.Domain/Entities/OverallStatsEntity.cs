namespace EAFCMatchTracker.Domain.Entities;

public class OverallStatsEntity
{
    public long Id { get; set; }
    public long ClubId { get; set; }
    public string? BestDivision { get; set; }
    public string? BestFinishGroup { get; set; }
    public string? GamesPlayed { get; set; }
    public string? GamesPlayedPlayoff { get; set; }
    public string? Goals { get; set; }
    public string? GoalsAgainst { get; set; }
    public string? Promotions { get; set; }
    public string? Relegations { get; set; }
    public string? Losses { get; set; }
    public string? Ties { get; set; }
    public string? Wins { get; set; }
    public string? Wstreak { get; set; }
    public string? Unbeatenstreak { get; set; }
    public string? SkillRating { get; set; }
    public string? Reputationtier { get; set; }
    public string? LeagueAppearances { get; set; }
    public int? CurrentDivision { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
