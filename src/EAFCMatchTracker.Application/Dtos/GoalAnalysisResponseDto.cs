namespace EAFCMatchTracker.Application.Dtos;

public class GoalAnalysisResponseDto
{
    public long ClubId { get; set; }
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public int TotalMatches { get; set; }
    public int TotalGoals { get; set; }
    public int LinkedGoals { get; set; }
    public int TotalAssists { get; set; }
    public int TotalPreAssists { get; set; }

    public List<GoalAnalysisPlayerDto> Players { get; set; } = new();
    public List<GoalAnalysisPairDto> Pairs { get; set; } = new();
    public List<GoalAnalysisTrioDto> Trios { get; set; } = new();
    public List<GoalAnalysisLinkDto> GoalLinks { get; set; } = new();
}

public class GoalAnalysisPlayerDto
{
    public string Name { get; set; } = "";
    public int Goals { get; set; }
    public int Assists { get; set; }
    public int PreAssists { get; set; }
    public int Total { get; set; }
}

public class GoalAnalysisPairDto
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public int Count { get; set; }
}

public class GoalAnalysisTrioDto
{
    public string Pre { get; set; } = "";
    public string Assist { get; set; } = "";
    public string Scorer { get; set; } = "";
    public int Count { get; set; }
}

public class GoalAnalysisLinkDto
{
    public long MatchId { get; set; }
    public DateTime MatchTimestamp { get; set; }
    public string ScorerName { get; set; } = "";
    public string? AssistName { get; set; }
    public string? PreAssistName { get; set; }
}
