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

public class OverallStats
{
    public string ClubId { get; set; }
    public string BestDivision { get; set; }
    public string BestFinishGroup { get; set; }
    public string FinishesInDivision1Group1 { get; set; }
    public string FinishesInDivision2Group1 { get; set; }
    public string FinishesInDivision3Group1 { get; set; }
    public string FinishesInDivision4Group1 { get; set; }
    public string FinishesInDivision5Group1 { get; set; }
    public string FinishesInDivision6Group1 { get; set; }
    public string GamesPlayed { get; set; }
    public string GamesPlayedPlayoff { get; set; }
    public string Goals { get; set; }
    public string GoalsAgainst { get; set; }
    public string Promotions { get; set; }
    public string Relegations { get; set; }
    public string Losses { get; set; }
    public string Ties { get; set; }
    public string Wins { get; set; }
    public string LastMatch0 { get; set; }
    public string LastMatch1 { get; set; }
    public string LastMatch2 { get; set; }
    public string LastMatch3 { get; set; }
    public string LastMatch4 { get; set; }
    public string LastMatch5 { get; set; }
    public string LastMatch6 { get; set; }
    public string LastMatch7 { get; set; }
    public string LastMatch8 { get; set; }
    public string LastMatch9 { get; set; }
    public string LastOpponent0 { get; set; }
    public string LastOpponent1 { get; set; }
    public string LastOpponent2 { get; set; }
    public string LastOpponent3 { get; set; }
    public string LastOpponent4 { get; set; }
    public string LastOpponent5 { get; set; }
    public string LastOpponent6 { get; set; }
    public string LastOpponent7 { get; set; }
    public string LastOpponent8 { get; set; }
    public string LastOpponent9 { get; set; }
    public string Wstreak { get; set; }
    public string Unbeatenstreak { get; set; }
    public string SkillRating { get; set; }
    public string Reputationtier { get; set; }
    public string LeagueAppearances { get; set; }
}

public class PlayoffAchievement
{
    public string SeasonId { get; set; }
    public string SeasonName { get; set; }
    public string BestDivision { get; set; }
    public string BestFinishGroup { get; set; }
}

public sealed class SearchClubResult
{
    public string clubId { get; set; } = default!;
    public string wins { get; set; } = default!;
    public string losses { get; set; } = default!;
    public string ties { get; set; } = default!;
    public string gamesPlayed { get; set; } = default!;
    public string gamesPlayedPlayoff { get; set; } = default!;
    public string goals { get; set; } = default!;
    public string goalsAgainst { get; set; } = default!;
    public string cleanSheets { get; set; } = default!;
    public string points { get; set; } = default!;
    public string reputationtier { get; set; } = default!;
    public string promotions { get; set; } = default!;
    public string relegations { get; set; } = default!;
    public string bestDivision { get; set; } = default!;
    public SearchClubInfo clubInfo { get; set; } = default!;
    public string platform { get; set; } = default!;
    public string clubName { get; set; } = default!;
    public string currentDivision { get; set; } = default!;
}

public sealed class SearchClubInfo
{
    public string name { get; set; } = default!;
    public long clubId { get; set; }
    public long regionId { get; set; }
    public long teamId { get; set; }
    public CustomKit? customKit { get; set; }
}

public sealed class MembersStatsResponse
{
    public List<MemberStats> members { get; set; } = new();
    public Dictionary<string, int>? positionCount { get; set; }
}

public sealed class MemberStats
{
    public string name { get; set; } = default!;
    public string proOverall { get; set; } = default!;
    public string proOverallStr { get; set; } = default!;
    public string proHeight { get; set; } = default!;
    public string proName { get; set; } = default!;
}
