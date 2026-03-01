namespace EAFCMatchTracker.Application.Dtos;

public class MatchPlayerDto
{
    public long PlayerId { get; set; }
    public long Id { get; set; }
    public long ClubId { get; set; }
    public string Playername { get; set; }
    public string Pos { get; set; }
    public short Namespace { get; set; }

    public short Goals { get; set; }
    public short Assists { get; set; }
    public short PreAssists { get; set; }
    public short Cleansheetsany { get; set; }
    public short Cleansheetsdef { get; set; }
    public short Cleansheetsgk { get; set; }
    public short Losses { get; set; }
    public bool Mom { get; set; }
    public short Passattempts { get; set; }
    public short Passesmade { get; set; }
    public double Rating { get; set; }
    public string Realtimegame { get; set; }
    public string Realtimeidle { get; set; }
    public short Redcards { get; set; }
    public short Saves { get; set; }
    public short Score { get; set; }
    public short Shots { get; set; }
    public short Tackleattempts { get; set; }
    public short Tacklesmade { get; set; }
    public string Vproattr { get; set; }
    public string Vprohackreason { get; set; }
    public short Wins { get; set; }

    public PlayerMatchStatsDto Stats { get; set; }
}
