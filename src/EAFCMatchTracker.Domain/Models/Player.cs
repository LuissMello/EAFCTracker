using System.Text.Json.Serialization;

namespace EAFCMatchTracker.Domain.Models;

public class Player
{
    public string Assists { get; set; }
    public string Cleansheetsany { get; set; }
    public string Cleansheetsdef { get; set; }
    public string Cleansheetsgk { get; set; }
    public string Goals { get; set; }
    public string Goalsconceded { get; set; }
    public string Losses { get; set; }
    public string Mom { get; set; }
    public string Namespace { get; set; }
    public string Passattempts { get; set; }
    public string Passesmade { get; set; }
    public string Pos { get; set; }
    public string Rating { get; set; }
    public string Realtimegame { get; set; }
    public string Realtimeidle { get; set; }
    public string Redcards { get; set; }
    public string Saves { get; set; }
    public string Score { get; set; }
    public string Shots { get; set; }
    public string Tackleattempts { get; set; }
    public string Tacklesmade { get; set; }
    public string Vproattr { get; set; }
    public string Vprohackreason { get; set; }
    public string Wins { get; set; }
    public string Playername { get; set; }

    public string Archetypeid { get; set; }
    public string BallDiveSaves { get; set; }
    public string CrossSaves { get; set; }
    public string GameTime { get; set; }
    public string GoodDirectionSaves { get; set; }

    [JsonPropertyName("match_event_aggregate_0")]
    public string MatchEventAggregate0 { get; set; }
    [JsonPropertyName("match_event_aggregate_1")]

    public string MatchEventAggregate1 { get; set; }
    [JsonPropertyName("match_event_aggregate_2")]

    public string MatchEventAggregate2 { get; set; }
    [JsonPropertyName("match_event_aggregate_3")]

    public string MatchEventAggregate3 { get; set; }
    public string ParrySaves { get; set; }
    public string PunchSaves { get; set; }
    public string ReflexSaves { get; set; }
    public string SecondsPlayed { get; set; }
    public string UserResult { get; set; }
}
