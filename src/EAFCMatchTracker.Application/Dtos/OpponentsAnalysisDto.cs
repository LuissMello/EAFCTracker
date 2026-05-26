namespace EAFCMatchTracker.Application.Dtos;

public class OpponentsAnalysisDto
{
    public int TotalMatches { get; set; }
    public int TotalOpponents { get; set; }
    public List<OpponentRecordDto> Opponents { get; set; } = new();
}

public class OpponentRecordDto
{
    public string Name { get; set; } = "";
    public long ClubId { get; set; }
    public int Matches { get; set; }
    public int Wins { get; set; }
    public int Draws { get; set; }
    public int Losses { get; set; }
    public double WinRate { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
    public int GoalDiff { get; set; }
    public long? BiggestWinMatchId { get; set; }
    public int BiggestWinGF { get; set; }
    public int BiggestWinGA { get; set; }
    public long? BiggestLossMatchId { get; set; }
    public int BiggestLossGF { get; set; }
    public int BiggestLossGA { get; set; }
    public DateTime? LastMatch { get; set; }
}
