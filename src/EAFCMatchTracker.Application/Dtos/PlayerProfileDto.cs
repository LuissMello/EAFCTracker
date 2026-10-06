namespace EAFCMatchTracker.Application.Dtos;

public class PlayerProfileDto
{
    public long PlayerEntityId { get; set; }
    public string Name { get; set; } = "";
    public string AccountName { get; set; } = "";
    public long PlayerId { get; set; }
    public long ClubId { get; set; }

    public int TotalMatches { get; set; }
    public int TotalWins { get; set; }
    public int TotalDraws { get; set; }
    public int TotalLosses { get; set; }
    public int TotalGoals { get; set; }
    public int TotalAssists { get; set; }
    public int TotalPreAssists { get; set; }
    public double AvgRating { get; set; }
    public int TotalMoM { get; set; }
    public int TotalRedCards { get; set; }
    public int TotalCleanSheets { get; set; }
    public int TotalSaves { get; set; }
    public int HatTricks { get; set; }

    public double BestRating { get; set; }
    public double WorstRating { get; set; }
    public long? BestRatingMatchId { get; set; }
    public long? WorstRatingMatchId { get; set; }
    public int MostGoalsInMatch { get; set; }
    public int MostAssistsInMatch { get; set; }

    public int? ProOverall { get; set; }

    public Dictionary<string, int> Positions { get; set; } = new();
    public List<PlayerMatchHistoryDto> History { get; set; } = new();

    /// <summary>Uso de cada arquétipo em todo o histórico (do mais usado para o menos usado).</summary>
    public List<ArchetypeUsage> Archetypes { get; set; } = new();
    /// <summary>Trocas reais de arquétipo em ordem cronológica (ignora partidas sem dado).</summary>
    public List<ArchetypeChange> ArchetypeChanges { get; set; } = new();

    /// <summary>Eco dos filtros (nulos = sem filtro). Filtram <c>History</c>, <c>Archetypes</c> e <c>FilteredSummary</c>; os totais do perfil não mudam.</summary>
    public string? PositionGroup { get; set; }
    public int? ArchetypeId { get; set; }
    /// <summary>Arquétipos do histórico COM a posição aplicada e SEM o filtro de arquétipo.</summary>
    public List<AvailableArchetypeDto> AvailableArchetypes { get; set; } = new();
    /// <summary>Posições do histórico, sem nenhum dos dois filtros.</summary>
    public List<AvailablePositionGroupDto> AvailablePositionGroups { get; set; } = new();
    /// <summary>Resumo das partidas que passam no filtro (igual aos totais quando não há filtro).</summary>
    public PlayerFilteredSummaryDto FilteredSummary { get; set; } = new();
}

public class PlayerFilteredSummaryDto
{
    public int Matches { get; set; }
    public int Wins { get; set; }
    public int Draws { get; set; }
    public int Losses { get; set; }
    public int Goals { get; set; }
    public int Assists { get; set; }
    public double? AvgRating { get; set; }
    public double? AvgProOverall { get; set; }
}

public class PlayerMatchHistoryDto
{
    public long MatchId { get; set; }
    public DateTime Timestamp { get; set; }
    public int Goals { get; set; }
    public int Assists { get; set; }
    public int PreAssists { get; set; }
    public double Rating { get; set; }
    public string Pos { get; set; } = "";
    public bool Mom { get; set; }
    public int SecondsPlayed { get; set; }
    public string Result { get; set; } = "";
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
    public string? OpponentName { get; set; }

    /// <summary>archetypeid da EA nesta partida (0 = sem dado).</summary>
    public short ArchetypeId { get; set; }
    public ArchetypeRef? Archetype { get; set; }
}
