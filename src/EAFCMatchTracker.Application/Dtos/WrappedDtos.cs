namespace EAFCMatchTracker.Application.Dtos;

public class WrappedDto
{
    public long ClubId { get; set; }
    public string ClubName { get; set; } = "";
    public int? GameVersion { get; set; }
    public string? GameVersionName { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public string TimeZoneId { get; set; } = "";
    public WrappedTotalsDto Totals { get; set; } = new();
    public WrappedStreaksDto Streaks { get; set; } = new();
    public WrappedBigMomentsDto BigMoments { get; set; } = new();
    public WrappedPlayersDto Players { get; set; } = new();
    public WrappedBestDuoDto? BestDuo { get; set; }
    public WrappedOpponentsDto Opponents { get; set; } = new();
    public WrappedRhythmDto Rhythm { get; set; } = new();
    public WrappedProgressionDto Progression { get; set; } = new();
    public List<string> FunFacts { get; set; } = new();
    public WrappedArchetypesDto Archetypes { get; set; } = new();
}

/// <summary>Arquétipos no nível do clube: o mais usado, quantas trocas houve (soma dos jogadores) e o uso de cada um.</summary>
public class WrappedArchetypesDto
{
    public ArchetypeUsage? MostUsed { get; set; }
    public int Switches { get; set; }
    public List<ArchetypeUsage> List { get; set; } = new();
}

public class WrappedTotalsDto
{
    public int Matches { get; set; }
    public int Wins { get; set; }
    public int Draws { get; set; }
    public int Losses { get; set; }
    public double WinRatePct { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
    public int CleanSheets { get; set; }
    public int Sessions { get; set; }
    public int ActiveDays { get; set; }
    public int EstimatedMinutes { get; set; }
}

public class WrappedStreakDto
{
    public int Length { get; set; }
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
}

public class WrappedStreaksDto
{
    public WrappedStreakDto? LongestWin { get; set; }
    public WrappedStreakDto? LongestUnbeaten { get; set; }
    public WrappedStreakDto? LongestWinless { get; set; }
    public WrappedStreakDto? LongestCleanSheet { get; set; }
}

public class WrappedSessionRefDto
{
    public long SessionId { get; set; }
    public DateOnly Date { get; set; }
    public int Wins { get; set; }
    public int Draws { get; set; }
    public int Losses { get; set; }
}

public class WrappedBigMomentsDto
{
    public MatchRefDto? BiggestWin { get; set; }
    public MatchRefDto? WorstLoss { get; set; }
    public MatchRefDto? HighestScoring { get; set; }
    public WrappedSessionRefDto? BestSession { get; set; }
    public WrappedSessionRefDto? WorstSession { get; set; }
}

public class WrappedPlayerStatDto
{
    public long PlayerEntityId { get; set; }
    public string Name { get; set; } = "";
    public double Value { get; set; }
    public int Matches { get; set; }

    /// <summary>Arquétipo principal do jogador no período; nulo sem dado.</summary>
    public ArchetypeRef? Archetype { get; set; }
    public List<ArchetypeUsage> Archetypes { get; set; } = new();
}

public class WrappedPlayersDto
{
    public WrappedPlayerStatDto? TopScorer { get; set; }
    public WrappedPlayerStatDto? TopAssister { get; set; }
    public WrappedPlayerStatDto? MostMotm { get; set; }
    public WrappedPlayerStatDto? BestAvgRating { get; set; }
    public WrappedPlayerStatDto? MostMatches { get; set; }
    public WrappedPlayerStatDto? MostRedCards { get; set; }
    public int HatTricks { get; set; }
}

public class WrappedBestDuoDto
{
    public string ScorerName { get; set; } = "";
    public string AssisterName { get; set; } = "";
    public int Goals { get; set; }
}

public class WrappedOpponentsDto
{
    public OpponentWdlDto? MostFaced { get; set; }
    public OpponentWdlDto? FavoriteVictim { get; set; }
    public OpponentWdlDto? Nemesis { get; set; }
}

public class WrappedWeekdayDto { public int Weekday { get; set; } public int Matches { get; set; } }
public class WrappedHourDto { public int Hour { get; set; } public int Matches { get; set; } }

public class WrappedBestMonthDto
{
    public string Month { get; set; } = "";
    public double WinRatePct { get; set; }
    public int Matches { get; set; }
}

public class WrappedMonthDto
{
    public string Month { get; set; } = "";
    public int Matches { get; set; }
    public int Wins { get; set; }
    public int Draws { get; set; }
    public int Losses { get; set; }
}

public class WrappedRhythmDto
{
    public WrappedWeekdayDto? BusiestWeekday { get; set; }
    public WrappedHourDto? BusiestHour { get; set; }
    public WrappedBestMonthDto? BestMonth { get; set; }
    public List<WrappedMonthDto> Monthly { get; set; } = new();
}

public class WrappedSrPointDto
{
    public DateOnly Date { get; set; }
    public int Value { get; set; }
}

public class WrappedSkillRatingDto
{
    public int? Start { get; set; }
    public int? End { get; set; }
    public WrappedSrPointDto? Peak { get; set; }
    public WrappedSrPointDto? Low { get; set; }
}

public class WrappedDivisionDto
{
    public int? Start { get; set; }
    public int? End { get; set; }
    public int Promotions { get; set; }
    public int Relegations { get; set; }
}

public class WrappedProgressionDto
{
    public WrappedSkillRatingDto SkillRating { get; set; } = new();
    public WrappedDivisionDto Division { get; set; } = new();
    public List<WrappedSrPointDto> SrSeries { get; set; } = new();
}
