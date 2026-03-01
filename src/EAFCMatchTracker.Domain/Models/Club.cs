using System.Text.Json.Serialization;

namespace EAFCMatchTracker.Domain.Models;

public class Club
{
    public string Date { get; set; }
    public string GameNumber { get; set; }
    public string Goals { get; set; }
    public string GoalsAgainst { get; set; }
    public string Losses { get; set; }
    public string MatchType { get; set; }
    public string Result { get; set; }
    public string Score { get; set; }

    [JsonPropertyName("season_id")]
    public string SeasonId { get; set; }

    public string TEAM { get; set; }
    public string Ties { get; set; }
    public string WinnerByDnf { get; set; }
    public string Wins { get; set; }
    public ClubDetails Details { get; set; }
}
