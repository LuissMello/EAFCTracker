namespace EAFCMatchTracker.Application.Dtos;

public class ClubStatisticsDto
{
    public long ClubId { get; set; }
    public string ClubName { get; set; }
    public string ClubCrestAssetId { get; set; }

    public int MatchesPlayed { get; set; }
    public int TotalGoals { get; set; }
    public int TotalGoalsConceded { get; set; }
    public int TotalAssists { get; set; }
    public int TotalShots { get; set; }
    public int TotalPassesMade { get; set; }
    public int TotalPassAttempts { get; set; }
    public int TotalTacklesMade { get; set; }
    public int TotalTackleAttempts { get; set; }
    public double TotalWins { get; set; }
    public double TotalLosses { get; set; }
    public double TotalDraws { get; set; }
    public int TotalCleanSheets { get; set; }
    public int TotalRedCards { get; set; }
    public int TotalSaves { get; set; }
    public int TotalMom { get; set; }

    public double AvgRating { get; set; }

    public double WinPercent { get; set; }
    public double PassAccuracyPercent { get; set; }
    public double TackleSuccessPercent { get; set; }
    public double GoalAccuracyPercent { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
}
