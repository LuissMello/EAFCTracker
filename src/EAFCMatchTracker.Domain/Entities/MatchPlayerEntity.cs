namespace EAFCMatchTracker.Domain.Entities;

public class MatchPlayerEntity
{
    public long MatchId { get; set; }
    public long PlayerEntityId { get; set; }
    public long ClubId { get; set; }
    public short Assists { get; set; }
    public short Cleansheetsany { get; set; }
    public short Cleansheetsdef { get; set; }
    public short Cleansheetsgk { get; set; }
    public short Goals { get; set; }
    public short Goalsconceded { get; set; }
    public short Losses { get; set; }
    public bool Mom { get; set; }
    public short Namespace { get; set; }
    public short Passattempts { get; set; }
    public short Passesmade { get; set; }
    public string Pos { get; set; } = default!;
    public double Rating { get; set; }
    public string Realtimegame { get; set; } = default!;
    public string Realtimeidle { get; set; } = default!;
    public short Redcards { get; set; }
    public short Saves { get; set; }
    public short Score { get; set; }
    public short Shots { get; set; }
    public short Tackleattempts { get; set; }
    public short Tacklesmade { get; set; }
    public string Vproattr { get; set; } = default!;
    public string Vprohackreason { get; set; } = default!;
    public short Wins { get; set; }
    public bool Disconnected { get; set; }

    public short Archetypeid { get; set; }
    public short BallDiveSaves { get; set; }
    public short CrossSaves { get; set; }
    public short GameTime { get; set; }
    public short GoodDirectionSaves { get; set; }

    public string MatchEventAggregate0 { get; set; } = default!;
    public string MatchEventAggregate1 { get; set; } = default!;
    public string MatchEventAggregate2 { get; set; } = default!;
    public string MatchEventAggregate3 { get; set; } = default!;

    public short ParrySaves { get; set; }
    public short PunchSaves { get; set; }
    public short ReflexSaves { get; set; }
    public short SecondsPlayed { get; set; }
    public short UserResult { get; set; }
    public int? ProOverall { get; set; }
    public string? ProOverallStr { get; set; }
    public int? ProHeight { get; set; }
    public string? ProName { get; set; }
    public short PreAssists { get; set; }

    public long PlayerMatchStatsEntityId { get; set; }

    public PlayerEntity Player { get; set; } = default!;
    public MatchEntity Match { get; set; } = default!;
    public PlayerMatchStatsEntity PlayerMatchStats { get; set; } = default!;
}
