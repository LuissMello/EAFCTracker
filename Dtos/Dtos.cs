public class MatchDto
{
    public long MatchId { get; set; }
    public DateTime Timestamp { get; set; }
    public MatchType MatchType { get; set; } // Incluído MatchType
    public List<MatchClubDto> Clubs { get; set; }
    public List<MatchPlayerDto> Players { get; set; }
}

public class MatchClubDto
{
    public long ClubId { get; set; }
    public DateTime Date { get; set; }
    public int GameNumber { get; set; }
    public short Goals { get; set; }
    public short GoalsAgainst { get; set; }
    public short Losses { get; set; }
    public short MatchType { get; set; }
    public short Result { get; set; }
    public short Score { get; set; }
    public short SeasonId { get; set; }
    public int Team { get; set; }
    public short Ties { get; set; }
    public short Wins { get; set; }
    public bool WinnerByDnf { get; set; }

    public ClubDetailsDto Details { get; set; }
}

public class ClubDetailsDto
{
    public string Name { get; set; }
    public long ClubId { get; set; }
    public long RegionId { get; set; }
    public long TeamId { get; set; }
    public string StadName { get; set; }

    // Propriedades de CustomKit
    public string KitId { get; set; }
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
    public string SelectedKitType { get; set; }
}

public class MatchPlayerDto
{
    public long PlayerId { get; set; }
    public long ClubId { get; set; }
    public string Playername { get; set; }
    public string Pos { get; set; }
    public short Namespace { get; set; }

    public short Goals { get; set; }
    public short Assists { get; set; }
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

public class PlayerMatchStatsDto
{
    public int Aceleracao { get; set; }
    public int Pique { get; set; }
    public int Finalizacao { get; set; }
    public int Falta { get; set; }
    public int Cabeceio { get; set; }
    public int ForcaDoChute { get; set; }
    public int ChuteLonge { get; set; }
    public int Voleio { get; set; }
    public int Penalti { get; set; }
    public int Visao { get; set; }
    public int Cruzamento { get; set; }
    public int Lancamento { get; set; }
    public int PasseCurto { get; set; }
    public int Curva { get; set; }
    public int Agilidade { get; set; }
    public int Equilibrio { get; set; }
    public int PosAtaqueInutil { get; set; }
    public int ControleBola { get; set; }
    public int Conducao { get; set; }
    public int Interceptacaos { get; set; }
    public int NocaoDefensiva { get; set; }
    public int DivididaEmPe { get; set; }
    public int Carrinho { get; set; }
    public int Impulsao { get; set; }
    public int Folego { get; set; }
    public int Forca { get; set; }
    public int Reacao { get; set; }
    public int Combatividade { get; set; }
    public int Frieza { get; set; }
    public int ElasticidadeGL { get; set; }
    public int ManejoGL { get; set; }
    public int ChuteGL { get; set; }
    public int ReflexosGL { get; set; }
    public int PosGL { get; set; }
}

public class MatchPlayerStatsDto
{
    public long PlayerId { get; set; }
    public string PlayerName { get; set; }
    public short Assists { get; set; }
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

public class PlayerStatisticsDto
{
    public long PlayerId { get; set; }
    public string PlayerName { get; set; }
    public long ClubId { get; set; }

    public int MatchesPlayed { get; set; }
    public int TotalGoals { get; set; }
    public int TotalAssists { get; set; }
    public int TotalShots { get; set; }
    public int TotalPassesMade { get; set; }
    public int TotalPassAttempts { get; set; }
    public int TotalTacklesMade { get; set; }
    public int TotalTackleAttempts { get; set; }
    public int TotalWins { get; set; }
    public int TotalLosses { get; set; }
    public int TotalDraws { get; set; }
    public int TotalCleanSheets { get; set; }
    public int TotalRedCards { get; set; }
    public int TotalSaves { get; set; }
    public int TotalMom { get; set; }

    public double AvgRating { get; set; }

    public double PassAccuracyPercent { get; set; }
    public double TackleSuccessPercent { get; set; }
    public double GoalAccuracyPercent { get; set; }
    public double WinPercent { get; set; }
}

public class ClubStatisticsDto
{
    public long ClubId { get; set; }
    public string ClubName { get; set; }
    public string ClubCrestAssetId { get; set; }

    public int MatchesPlayed { get; set; }
    public int TotalGoals { get; set; }
    public int TotalAssists { get; set; }
    public int TotalShots { get; set; }
    public int TotalPassesMade { get; set; }
    public int TotalPassAttempts { get; set; }
    public int TotalTacklesMade { get; set; }
    public int TotalTackleAttempts { get; set; }
    public double TotalWins { get; set; }
    public double TotalLosses { get; set; }
    public double TotalDraws { get; set; }
    public int TotalCleanSheets { get; set; }
    public int TotalRedCards { get; set; }
    public int TotalSaves { get; set; }
    public int TotalMom { get; set; }

    public double AvgRating { get; set; }

    public double WinPercent { get; set; }
    public double PassAccuracyPercent { get; set; }
    public double TackleSuccessPercent { get; set; }
    public double GoalAccuracyPercent { get; set; }
}

public class MatchStatisticsDto
{
    public int TotalMatches { get; set; }
    public int TotalPlayers { get; set; }

    public int TotalGoals { get; set; }
    public int TotalAssists { get; set; }
    public int TotalShots { get; set; }
    public int TotalPassesMade { get; set; }
    public int TotalPassAttempts { get; set; }
    public int TotalTacklesMade { get; set; }
    public int TotalTackleAttempts { get; set; }
    public double TotalRating { get; set; }
    public int TotalWins { get; set; }
    public int TotalLosses { get; set; }
    public int TotalDraws { get; set; }
    public int TotalCleanSheets { get; set; }
    public int TotalRedCards { get; set; }
    public int TotalSaves { get; set; }
    public int TotalMom { get; set; }

    public double AvgGoals { get; set; }
    public double AvgAssists { get; set; }
    public double AvgShots { get; set; }
    public double AvgPassesMade { get; set; }
    public double AvgPassAttempts { get; set; }
    public double AvgTacklesMade { get; set; }
    public double AvgTackleAttempts { get; set; }
    public double AvgRating { get; set; }
    public double AvgRedCards { get; set; }
    public double AvgSaves { get; set; }
    public double AvgMom { get; set; }

    public double WinPercent { get; set; }
    public double LossPercent { get; set; }
    public double DrawPercent { get; set; }
    public double CleanSheetsPercent { get; set; }
    public double MomPercent { get; set; }
    public double PassAccuracyPercent { get; set; }
    public double TackleSuccessPercent { get; set; }
    public double GoalAccuracyPercent { get; set; }
}

public class FullMatchStatisticsDto
{
    public MatchStatisticsDto Overall { get; set; }
    public List<PlayerStatisticsDto> Players { get; set; }
    public List<ClubStatisticsDto> Clubs { get; set; }
}

public class MatchResultDto
{
    public long MatchId { get; set; }
    public DateTime Timestamp { get; set; }

    public string ClubAName { get; set; }
    public short ClubAGoals { get; set; }
    public short ClubARedCards { get; set; }   
    public int ClubAPlayerCount { get; set; }
    public ClubDetailsDto ClubADetails { get; set; }

    public string ClubBName { get; set; }
    public short ClubBGoals { get; set; }
    public short ClubBRedCards { get; set; }  
    public int ClubBPlayerCount { get; set; }
    public ClubDetailsDto ClubBDetails { get; set; }

    public string ResultText { get; set; }
}

public class CalendarDaySummaryDto
{
    public DateOnly Date { get; set; }
    public int MatchesCount { get; set; }
    public int Wins { get; set; }
    public int Draws { get; set; }
    public int Losses { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
}

public class CalendarMonthDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public List<CalendarDaySummaryDto> Days { get; set; } = new();
}

public class CalendarMatchStatLineDto
{
    public int TotalGoals { get; set; }
    public int TotalAssists { get; set; }
    public int TotalShots { get; set; }
    public int TotalPassesMade { get; set; }
    public int TotalPassAttempts { get; set; }
    public int TotalTacklesMade { get; set; }
    public int TotalTackleAttempts { get; set; }
    public int TotalRedCards { get; set; }
    public int TotalSaves { get; set; }
    public int TotalMom { get; set; }

    public double PassAccuracyPercent { get; set; }
    public double TackleSuccessPercent { get; set; }
    public double GoalAccuracyPercent { get; set; }
    public double AvgRating { get; set; }
}

public class CalendarMatchSummaryDto
{
    public long MatchId { get; set; }
    public DateTime Timestamp { get; set; }

    // lado A
    public long ClubAId { get; set; }
    public string ClubAName { get; set; }
    public int ClubAGoals { get; set; }
    public string? ClubACrestAssetId { get; set; }

    // lado B
    public long ClubBId { get; set; }
    public string ClubBName { get; set; }
    public int ClubBGoals { get; set; }
    public string? ClubBCrestAssetId { get; set; }

    // resultado do ponto de vista do clubId consultado
    // "W" = vitória, "D" = empate, "L" = derrota
    public string ResultForClub { get; set; }

    // estatísticas agregadas do jogo (soma dos jogadores)
    public CalendarMatchStatLineDto Stats { get; set; }
}

public class CalendarDayDetailsDto
{
    public DateOnly Date { get; set; }
    public int TotalMatches { get; set; }
    public int Wins { get; set; }
    public int Draws { get; set; }
    public int Losses { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
    public List<CalendarMatchSummaryDto> Matches { get; set; } = new();
}

public sealed class ClubListItemDto
{
    public long ClubId { get; init; }
    public string Name { get; init; } = default!;
    public string? CrestAssetId { get; init; }
}