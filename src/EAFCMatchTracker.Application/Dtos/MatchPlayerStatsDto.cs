namespace EAFCMatchTracker.Application.Dtos;

public class MatchPlayerStatsDto
{
    public long PlayerId { get; set; }
    public string PlayerName { get; set; }
    public short Assists { get; set; }
    public short PreAssists { get; set; }
    public short CleansheetsAny { get; set; }
    public short CleansheetsDef { get; set; }
    public short CleansheetsGk { get; set; }
    public short Goals { get; set; }
    public short GoalsConceded { get; set; }
    public short Losses { get; set; }
    public bool Mom { get; set; }
    public short Namespace { get; set; }
    public short PassAttempts { get; set; }
    public short PassesMade { get; set; }
    public double PassAccuracy { get; set; }
    public string Position { get; set; }
    public double Rating { get; set; }
    public string RealtimeGame { get; set; }
    public string RealtimeIdle { get; set; }
    public short RedCards { get; set; }
    public short Saves { get; set; }
    public short Score { get; set; }
    public short Shots { get; set; }
    public short TackleAttempts { get; set; }
    public short TacklesMade { get; set; }
    public string VproAttr { get; set; }
    public string VproHackReason { get; set; }
    public short Wins { get; set; }

    public PlayerMatchStatsDto? Statistics { get; set; }
}
