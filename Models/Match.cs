using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

public class TimeAgo
{
    public int Number { get; set; }
    public string Unit { get; set; }
}

public class CustomKit
{
    public string StadName { get; set; }
    public string KitId { get; set; }
    public string SeasonalTeamId { get; set; }
    public string SeasonalKitId { get; set; }
    public string SelectedKitType { get; set; }
    public string CustomKitId { get; set; }
    public string CustomAwayKitId { get; set; }
    public string CustomThirdKitId { get; set; }
    public string CustomKeeperKitId { get; set; }
    public string KitColor1 { get; set; }
    public string KitColor2 { get; set; }
    public string KitColor3 { get; set; }
    public string KitColor4 { get; set; }
    public string KitAColor1 { get; set; }
    public string KitAColor2 { get; set; }
    public string KitAColor3 { get; set; }
    public string KitAColor4 { get; set; }
    public string KitThrdColor1 { get; set; }
    public string KitThrdColor2 { get; set; }
    public string KitThrdColor3 { get; set; }
    public string KitThrdColor4 { get; set; }
    public string DCustomKit { get; set; }
    public string CrestColor { get; set; }
    public string CrestAssetId { get; set; }
}

public class ClubDetails
{
    public string Name { get; set; }
    public long ClubId { get; set; }
    public long RegionId { get; set; }
    public long TeamId { get; set; }
    public CustomKit CustomKit { get; set; }
}

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
}

public class AggregateStats
{
    public int Assists { get; set; }
    public int Cleansheetsany { get; set; }
    public int Cleansheetsdef { get; set; }
    public int Cleansheetsgk { get; set; }
    public int Goals { get; set; }
    public int Goalsconceded { get; set; }
    public int Losses { get; set; }
    public int Mom { get; set; }
    public int Namespace { get; set; }
    public int Passattempts { get; set; }
    public int Passesmade { get; set; }
    public int Pos { get; set; }
    public double Rating { get; set; }
    public int Realtimegame { get; set; }
    public int Realtimeidle { get; set; }
    public int Redcards { get; set; }
    public int Saves { get; set; }
    public int Score { get; set; }
    public int Shots { get; set; }
    public int Tackleattempts { get; set; }
    public int Tacklesmade { get; set; }
    public int Vproattr { get; set; }
    public int Vprohackreason { get; set; }
    public int Wins { get; set; }
}

public class Match
{
    public required string MatchId { get; set; }
    public long Timestamp { get; set; }
    public required TimeAgo TimeAgo { get; set; }

    // ClubId como chave dinâmica
    public required Dictionary<string, Club> Clubs { get; set; }

    // ClubId -> PlayerId -> Player
    public required Dictionary<string, Dictionary<string, Player>> Players { get; set; }

    // Estatísticas agregadas por clube
    public required Dictionary<string, AggregateStats> Aggregate { get; set; }
}
