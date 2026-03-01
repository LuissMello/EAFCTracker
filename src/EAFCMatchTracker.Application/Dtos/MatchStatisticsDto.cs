namespace EAFCMatchTracker.Application.Dtos;

public class MatchStatisticsDto
{
    public int TotalMatches { get; set; }
    public int TotalPlayers { get; set; }

    public int TotalGoals { get; set; }
    public int TotalAssists { get; set; }
    public int TotalPreAssists { get; set; }
    public int TotalShots { get; set; }
    public int TotalPassesMade { get; set; }
    public int TotalPassAttempts { get; set; }
    public int TotalTacklesMade { get; set; }
    public int TotalTackleAttempts { get; set; }
    public double TotalRating { get; set; }
    public int TotalWins { get; set; }
    public int TotalLosses { get; set; }
    public int TotalDraws { get; set; }
    public int TotalCleanSheets { get; set; }
    public int TotalRedCards { get; set; }
    public int TotalSaves { get; set; }
    public int TotalMom { get; set; }

    public double AvgGoals { get; set; }
    public double AvgAssists { get; set; }
    public double AvgPreAssists { get; set; }
    public double AvgShots { get; set; }
    public double AvgPassesMade { get; set; }
    public double AvgPassAttempts { get; set; }
    public double AvgTacklesMade { get; set; }
    public double AvgTackleAttempts { get; set; }
    public double AvgRating { get; set; }
    public double AvgRedCards { get; set; }
    public double AvgSaves { get; set; }
    public double AvgMom { get; set; }

    public double WinPercent { get; set; }
    public double LossPercent { get; set; }
    public double DrawPercent { get; set; }
    public double CleanSheetsPercent { get; set; }
    public double MomPercent { get; set; }
    public double PassAccuracyPercent { get; set; }
    public double TackleSuccessPercent { get; set; }
    public double GoalAccuracyPercent { get; set; }
}
