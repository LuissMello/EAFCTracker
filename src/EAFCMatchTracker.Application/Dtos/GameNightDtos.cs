namespace EAFCMatchTracker.Application.Dtos;

public class GameNightListDto
{
    public long ClubId { get; set; }
    public string TimeZoneId { get; set; } = "";
    public List<GameNightSummaryDto> Nights { get; set; } = new();
}

public class GameNightSummaryDto
{
    public long SessionId { get; set; }
    public DateOnly Date { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime EndedAtUtc { get; set; }
    public int Matches { get; set; }
    public int Wins { get; set; }
    public int Draws { get; set; }
    public int Losses { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
}

public class GameNightDetailDto
{
    public long ClubId { get; set; }
    public string ClubName { get; set; } = "";
    public long SessionId { get; set; }
    public DateOnly Date { get; set; }
    public string TimeZoneId { get; set; } = "";
    public DateTime StartedAtUtc { get; set; }
    public DateTime EndedAtUtc { get; set; }
    public int DurationMinutes { get; set; }
    public long? PrevSessionId { get; set; }
    public long? NextSessionId { get; set; }
    public int Index { get; set; }
    public int Total { get; set; }
    public GameNightRecordDto Record { get; set; } = new();
    public GameNightSkillRatingDto SkillRating { get; set; } = new();
    public GameNightDivisionDto Division { get; set; } = new();
    public GameNightHighlightsDto Highlights { get; set; } = new();
    public List<GameNightPlayerDto> Players { get; set; } = new();
    public List<OpponentWdlDto> Opponents { get; set; } = new();
    public List<GameNightMatchDto> Matches { get; set; } = new();
}

public class GameNightRecordDto
{
    public int Matches { get; set; }
    public int Wins { get; set; }
    public int Draws { get; set; }
    public int Losses { get; set; }
    public double WinRatePct { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
    public int GoalDiff { get; set; }
    public int CleanSheets { get; set; }
}

public class GameNightSkillRatingDto
{
    public int? Start { get; set; }
    public int? End { get; set; }
    public int? Delta { get; set; }
}

public class GameNightDivisionDto
{
    public int? Start { get; set; }
    public int? End { get; set; }
}

public class MatchRefDto
{
    public long MatchId { get; set; }
    public string OpponentName { get; set; } = "";
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
}

public class NamedPlayerDto
{
    public long PlayerEntityId { get; set; }
    public string Name { get; set; } = "";
}

public class PlayerGoalsDto : NamedPlayerDto { public int Goals { get; set; } }
public class PlayerAssistsDto : NamedPlayerDto { public int Assists { get; set; } }
public class PlayerRatingDto : NamedPlayerDto { public double AvgRating { get; set; } public int Matches { get; set; } }
public class PlayerCountDto : NamedPlayerDto { public int Count { get; set; } }
public class NightHatTrickDto : NamedPlayerDto { public long MatchId { get; set; } }

public class GameNightHighlightsDto
{
    public MatchRefDto? BiggestWin { get; set; }
    public MatchRefDto? WorstLoss { get; set; }
    public PlayerGoalsDto? TopScorer { get; set; }
    public PlayerAssistsDto? TopAssister { get; set; }
    public PlayerRatingDto? BestRated { get; set; }
    public PlayerCountDto? ManOfTheMatch { get; set; }
    public List<NightHatTrickDto> HatTricks { get; set; } = new();
    public int RedCards { get; set; }
}

public class GameNightPlayerDto : NamedPlayerDto
{
    public string Position { get; set; } = "";
    public int Matches { get; set; }
    public int Goals { get; set; }
    public int Assists { get; set; }
    public int PreAssists { get; set; }
    public double AvgRating { get; set; }
    public int Motm { get; set; }
    public int RedCards { get; set; }
}

/// <summary>Retrospecto contra um adversário (noite de jogo e retrospectiva).</summary>
public class OpponentWdlDto
{
    public long OpponentClubId { get; set; }
    public string Name { get; set; } = "";
    public string? CrestAssetId { get; set; }
    public string? CustomCrestAssetId { get; set; }
    public int Matches { get; set; }
    public int Wins { get; set; }
    public int Draws { get; set; }
    public int Losses { get; set; }
}

public class GameNightGoalDto
{
    public string ScorerName { get; set; } = "";
    public string? AssistName { get; set; }
    public string? PreAssistName { get; set; }
}

public class GameNightMatchDto
{
    public long MatchId { get; set; }
    public DateTime Timestamp { get; set; }
    public long OpponentClubId { get; set; }
    public string OpponentName { get; set; } = "";
    public string? CrestAssetId { get; set; }
    public string? CustomCrestAssetId { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
    public string Result { get; set; } = "";
    public int OurPlayersCount { get; set; }
    public int OpponentPlayersCount { get; set; }
    public int? SkillRatingAfter { get; set; }
    public List<GameNightGoalDto> Goals { get; set; } = new();
}
