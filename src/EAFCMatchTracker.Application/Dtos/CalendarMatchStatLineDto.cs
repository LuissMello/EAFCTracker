namespace EAFCMatchTracker.Application.Dtos;

public class CalendarMatchStatLineDto
{
    public int TotalGoals { get; set; }
    public int TotalAssists { get; set; }
    public int TotalPreAssists { get; set; }
    public int TotalShots { get; set; }
    public int TotalPassesMade { get; set; }
    public int TotalPassAttempts { get; set; }
    public int TotalTacklesMade { get; set; }
    public int TotalTackleAttempts { get; set; }
    public int TotalRedCards { get; set; }
    public int TotalSaves { get; set; }
    public int TotalMom { get; set; }

    public double PassAccuracyPercent { get; set; }
    public double TackleSuccessPercent { get; set; }
    public double GoalAccuracyPercent { get; set; }
    public double AvgRating { get; set; }
}
