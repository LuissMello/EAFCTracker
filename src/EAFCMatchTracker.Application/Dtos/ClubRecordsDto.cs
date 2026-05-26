namespace EAFCMatchTracker.Application.Dtos;

public class ClubRecordsDto
{
    public int TotalMatches { get; set; }
    public int TotalWins { get; set; }
    public int TotalDraws { get; set; }
    public int TotalLosses { get; set; }
    public int TotalGoalsFor { get; set; }
    public int TotalGoalsAgainst { get; set; }

    public RecordMatchDto? BiggestWin { get; set; }
    public RecordMatchDto? BiggestLoss { get; set; }
    public RecordMatchDto? HighestScoringMatch { get; set; }

    public int LongestWinStreak { get; set; }
    public int LongestUnbeatenStreak { get; set; }
    public int LongestCleanSheetStreak { get; set; }
    public int LongestScoringStreak { get; set; }
    public int CurrentWinStreak { get; set; }
    public int CurrentUnbeatenStreak { get; set; }

    public RecordPlayerMatchDto? MostGoalsInMatch { get; set; }
    public RecordPlayerMatchDto? MostAssistsInMatch { get; set; }
    public RecordPlayerMatchDto? MostSavesInMatch { get; set; }
    public RecordPlayerMatchDto? HighestRating { get; set; }
    public RecordPlayerMatchDto? MostRedCardsCareer { get; set; }
    public RecordPlayerMatchDto? MostMoMCareer { get; set; }
    public List<HatTrickDto> HatTricks { get; set; } = new();
}

public class RecordMatchDto
{
    public long MatchId { get; set; }
    public DateTime Timestamp { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
    public string? OpponentName { get; set; }
}

public class RecordPlayerMatchDto
{
    public long MatchId { get; set; }
    public DateTime Timestamp { get; set; }
    public string PlayerName { get; set; } = "";
    public int Value { get; set; }
}

public class HatTrickDto
{
    public long MatchId { get; set; }
    public DateTime Timestamp { get; set; }
    public string PlayerName { get; set; } = "";
    public int Goals { get; set; }
}
