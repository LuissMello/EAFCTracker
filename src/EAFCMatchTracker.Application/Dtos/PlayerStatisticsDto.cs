namespace EAFCMatchTracker.Application.Dtos;

public class PlayerStatisticsDto
{
    public DateTime Date { get; set; }
    public long PlayerId { get; set; }
    public string PlayerName { get; set; }
    public long ClubId { get; set; }

    public int MatchesPlayed { get; set; }
    public int TotalGoals { get; set; }
    public int TotalGoalsConceded { get; set; }
    public int TotalAssists { get; set; }
    public int TotalPreAssists { get; set; }
    public int TotalShots { get; set; }
    public int TotalPassesMade { get; set; }
    public int TotalPassAttempts { get; set; }
    public int TotalTacklesMade { get; set; }
    public int TotalTackleAttempts { get; set; }
    public int TotalWins { get; set; }
    public int TotalLosses { get; set; }
    public int TotalDraws { get; set; }
    public int TotalCleanSheets { get; set; }
    public int TotalRedCards { get; set; }
    public int TotalSaves { get; set; }
    public int TotalMom { get; set; }

    public double AvgRating { get; set; }

    public double PassAccuracyPercent { get; set; }
    public double TackleSuccessPercent { get; set; }
    public double GoalAccuracyPercent { get; set; }
    public double WinPercent { get; set; }
    public string? ProOverallStr { get; set; }
    public int? ProHeight { get; set; }
    public string? ProName { get; set; }
    public bool Disconnected { get; set; }
    public int TotalSecondsPlayed { get; set; }
    public int TotalGameTime { get; set; }
}
