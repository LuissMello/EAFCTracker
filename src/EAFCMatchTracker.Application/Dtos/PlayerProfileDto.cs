namespace EAFCMatchTracker.Application.Dtos;

public class PlayerProfileDto
{
    public long PlayerEntityId { get; set; }
    public string Name { get; set; } = "";
    public string AccountName { get; set; } = "";
    public long PlayerId { get; set; }
    public long ClubId { get; set; }

    public int TotalMatches { get; set; }
    public int TotalWins { get; set; }
    public int TotalDraws { get; set; }
    public int TotalLosses { get; set; }
    public int TotalGoals { get; set; }
    public int TotalAssists { get; set; }
    public int TotalPreAssists { get; set; }
    public double AvgRating { get; set; }
    public int TotalMoM { get; set; }
    public int TotalRedCards { get; set; }
    public int TotalCleanSheets { get; set; }
    public int TotalSaves { get; set; }
    public int HatTricks { get; set; }

    public double BestRating { get; set; }
    public double WorstRating { get; set; }
    public long? BestRatingMatchId { get; set; }
    public long? WorstRatingMatchId { get; set; }
    public int MostGoalsInMatch { get; set; }
    public int MostAssistsInMatch { get; set; }

    public int? ProOverall { get; set; }

    public Dictionary<string, int> Positions { get; set; } = new();
    public List<PlayerMatchHistoryDto> History { get; set; } = new();
}

public class PlayerMatchHistoryDto
{
    public long MatchId { get; set; }
    public DateTime Timestamp { get; set; }
    public int Goals { get; set; }
    public int Assists { get; set; }
    public int PreAssists { get; set; }
    public double Rating { get; set; }
    public string Pos { get; set; } = "";
    public bool Mom { get; set; }
    public int SecondsPlayed { get; set; }
    public string Result { get; set; } = "";
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
    public string? OpponentName { get; set; }
}
