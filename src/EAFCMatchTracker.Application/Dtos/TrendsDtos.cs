namespace EAFCMatchTracker.Application.Dtos;

public class MatchTrendPointDto
{
    public long MatchId { get; set; }
    public DateTime Timestamp { get; set; }
    public long OpponentClubId { get; set; }
    public string OpponentName { get; set; } = "";
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
    public string Result { get; set; } = ""; // "W" | "D" | "L"
    public int Shots { get; set; }
    public int PassesMade { get; set; }
    public int PassAttempts { get; set; }
    public double PassAccuracyPercent { get; set; }
    public int TacklesMade { get; set; }
    public int TackleAttempts { get; set; }
    public double TackleSuccessPercent { get; set; }
    public double AvgRating { get; set; }
    public bool MomOccurred { get; set; }
}

public class ClubTrendsDto
{
    public long ClubId { get; set; }
    public string ClubName { get; set; } = "";
    public List<MatchTrendPointDto> Series { get; set; } = new();

    public string FormLast5 { get; set; } = "";
    public string FormLast10 { get; set; } = "";

    public int CurrentUnbeaten { get; set; }
    public int CurrentWins { get; set; }
    public int CurrentCleanSheets { get; set; }

    public List<double> MovingAvgPassAcc_5 { get; set; } = new();
    public List<double> MovingAvgRating_5 { get; set; } = new();
    public List<double> MovingAvgTackleAcc_5 { get; set; } = new();
}

public class TopScorerItemDto
{
    public long PlayerEntityId { get; set; }
    public long PlayerId { get; set; }
    public string PlayerName { get; set; } = "";
    public long ClubId { get; set; }
    public int Goals { get; set; }
    public int Assists { get; set; }
    public int Matches { get; set; }
    public double AvgRating { get; set; }
    public int Mom { get; set; }
}
