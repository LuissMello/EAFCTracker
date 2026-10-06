namespace EAFCMatchTracker.Application.Dtos;

/// <summary>Eixos (0..99) da carta. <c>Ata</c> é nulo para goleiros; <c>Gol</c> é nulo para os demais.</summary>
public class PlayerCardAxesDto
{
    public int? Ata { get; set; }
    public int Pas { get; set; }
    public int Cri { get; set; }
    public int Def { get; set; }
    public int Imp { get; set; }
    public int Reg { get; set; }
    public int? Gol { get; set; }
}

public class PlayerCardStatsDto
{
    public int Goals { get; set; }
    public int Assists { get; set; }
    public int PreAssists { get; set; }
    public double GoalsPerMatch { get; set; }
    public double AssistsPerMatch { get; set; }
    public double ShotAccuracyPct { get; set; }
    public double PassAccuracyPct { get; set; }
    public double TackleAccuracyPct { get; set; }
    /// <summary>Só para goleiros.</summary>
    public double? SavePct { get; set; }
    public double AvgRating { get; set; }
    public int Motm { get; set; }
    public int RedCards { get; set; }
    /// <summary>Só para goleiros.</summary>
    public int? CleanSheets { get; set; }
}

public class PlayerCardAttributesDto
{
    public int? Overall { get; set; }
    public string? Height { get; set; }
}

public class PlayerCardDto
{
    public long PlayerEntityId { get; set; }
    public string Name { get; set; } = "";
    public string Position { get; set; } = "";
    /// <summary>"ATAQUE" | "MEIO" | "DEFESA" | "GOLEIRO".</summary>
    public string PositionGroup { get; set; } = "MEIO";
    public int Matches { get; set; }
    public int Minutes { get; set; }
    /// <summary>"bronze" | "prata" | "ouro" | "elite".</summary>
    public string Tier { get; set; } = "bronze";
    public int Overall { get; set; }
    /// <summary>"archetype" = overall com os pesos do arquétipo (carta de UM arquétipo); "position" = pesos do grupo da posição.</summary>
    public string Scoring { get; set; } = "position";
    /// <summary>Overall das MESMAS partidas com os pesos do grupo da posição (igual a <c>overall</c> quando scoring = "position").</summary>
    public int OverallByPosition { get; set; }
    /// <summary>Só em <c>view=archetype</c>: "{playerEntityId}-{archetypeId}" (archetypeId 0 = partidas sem dado).</summary>
    public string? SegmentKey { get; set; }
    public bool Provisional { get; set; }
    public PlayerCardAxesDto Axes { get; set; } = new();
    public PlayerCardStatsDto Stats { get; set; } = new();
    /// <summary>Últimas 5 notas, da mais recente para a mais antiga.</summary>
    public List<double> Form { get; set; } = new();
    public PlayerCardAttributesDto? Attributes { get; set; }
    public DateTime? LastPlayedAt { get; set; }

    /// <summary>Arquétipo principal das partidas consideradas na carta (com filtro, o próprio arquétipo filtrado).</summary>
    public ArchetypeRef? Archetype { get; set; }
    /// <summary>Uso de arquétipos do jogador no período (SEM o filtro de arquétipo), do mais usado ao menos usado.</summary>
    public List<ArchetypeUsage> Archetypes { get; set; } = new();
}

public class PlayerCardsDto
{
    public long ClubId { get; set; }
    public string ClubName { get; set; } = "";
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public int? GameVersion { get; set; }
    public int TotalMatches { get; set; }
    public int MinMatches { get; set; }
    public List<PlayerCardDto> Cards { get; set; } = new();

    /// <summary>Eco do filtro <c>archetypeId</c> (nulo sem filtro).</summary>
    public int? ArchetypeId { get; set; }
    /// <summary>Eco do filtro <c>playerEntityId</c> (nulo sem filtro). Com ele, <c>minMatches</c> não esconde as cartas desse jogador.</summary>
    public long? PlayerEntityId { get; set; }
    /// <summary>Todos os jogadores com ao menos 1 linha no período/edição, por nome; IGNORA positionGroup, archetypeId, playerEntityId e minMatches (matches = total de linhas dele no recorte).</summary>
    public List<AvailablePlayerDto> AvailablePlayers { get; set; } = new();
    /// <summary>"player" (uma carta por jogador) ou "archetype" (uma carta por jogador × arquétipo).</summary>
    public string View { get; set; } = "player";
    /// <summary>Eco do filtro <c>positionGroup</c> (nulo sem filtro).</summary>
    public string? PositionGroup { get; set; }
    /// <summary>Arquétipos observados no período COM a posição aplicada e SEM o filtro de arquétipo (opções do 2º filtro).</summary>
    public List<AvailableArchetypeDto> AvailableArchetypes { get; set; } = new();
    /// <summary>Posições do período, sem nenhum dos dois filtros.</summary>
    public List<AvailablePositionGroupDto> AvailablePositionGroups { get; set; } = new();
}

public class PlayerCompareMetricDto
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public double? A { get; set; }
    public double? B { get; set; }
    public bool HigherIsBetter { get; set; }
    /// <summary>"a" | "b" | "tie" | null (sem dado em um dos lados).</summary>
    public string? Winner { get; set; }
}

public class PlayerRatingPointDto
{
    public long MatchId { get; set; }
    public DateTime Timestamp { get; set; }
    public double? A { get; set; }
    public double? B { get; set; }
}

public class PlayerCompareDto
{
    public long ClubId { get; set; }
    /// <summary>Eco do filtro <c>archetypeId</c> (nulo sem filtro).</summary>
    public int? ArchetypeId { get; set; }
    /// <summary>Arquétipo usado em cada lado (<c>archetypeA</c>/<c>archetypeB</c>, ou <c>archetypeId</c> para os dois).</summary>
    public int? ArchetypeIdA { get; set; }
    public int? ArchetypeIdB { get; set; }
    public string? PositionGroup { get; set; }
    public PlayerCardDto A { get; set; } = new();
    public PlayerCardDto B { get; set; } = new();
    public List<PlayerCompareMetricDto> Metrics { get; set; } = new();
    /// <summary>Resultados do clube nas partidas em que os DOIS jogaram; nulo se nunca jogaram juntos.</summary>
    public LabStatsDto? Together { get; set; }
    /// <summary>Resultados do clube quando só A jogou; nulo se não houve.</summary>
    public LabStatsDto? OnlyA { get; set; }
    public LabStatsDto? OnlyB { get; set; }
    public List<PlayerRatingPointDto> RatingSeries { get; set; } = new();
}
