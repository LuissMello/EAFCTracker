using System.Text.Json.Serialization;

namespace EAFCMatchTracker.Application.Dtos;

/// <summary>
/// Referência a um arquétipo dentro de qualquer payload. <see cref="Label"/> já vem pronto: nome do catálogo ou
/// "Arquétipo #id". <see cref="PositionGroup"/> vem do catálogo ou, sem definição, é INFERIDO pela posição mais
/// jogada nesse id ("ATAQUE" | "MEIO" | "DEFESA" | "GOLEIRO"; nulo se nunca houve posição).
/// </summary>
public class ArchetypeRef
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string Label { get; set; } = "";
    public string? ShortName { get; set; }
    public string? PositionGroup { get; set; }

    public static string DefaultLabel(int id) => $"Arquétipo #{id}";

    /// <summary>Referência sem catálogo (rótulo padrão); os serviços a trocam pela do catálogo antes de responder.</summary>
    public static ArchetypeRef Unresolved(int id) => new() { Id = id, Label = DefaultLabel(id) };
}

/// <summary>Uso de um arquétipo por um jogador (ou, no Retrospectiva, pelo clube) num recorte.</summary>
public class ArchetypeUsage
{
    public ArchetypeRef Archetype { get; set; } = new();
    public int Matches { get; set; }
    /// <summary>% dos jogos do recorte (0–100; o denominador inclui jogos sem arquétipo).</summary>
    public double Pct { get; set; }
    public double? AvgRating { get; set; }
    /// <summary>Média de ProOverall (ou ProOverallStr numérico) ignorando nulos/0.</summary>
    public double? AvgProOverall { get; set; }
    public int Goals { get; set; }
    public int Assists { get; set; }
    public DateTime? FirstPlayedAt { get; set; }
    public DateTime? LastPlayedAt { get; set; }
}

/// <summary>Troca real de arquétipo (ignora partidas sem dado).</summary>
public class ArchetypeChange
{
    public DateTime At { get; set; }
    public long MatchId { get; set; }
    public ArchetypeRef? From { get; set; }
    public ArchetypeRef To { get; set; } = new();
}

/// <summary>Opção do filtro de cartas: arquétipos observados no período (calculado SEM o filtro de arquétipo).</summary>
public class AvailableArchetypeDto
{
    public ArchetypeRef Archetype { get; set; } = new();
    public int Players { get; set; }
    public int Matches { get; set; }
}

/// <summary>Opção do filtro de jogador: jogadores com ao menos 1 linha no recorte (com posição/arquétipo aplicados), ordenados por nome.</summary>
public class AvailablePlayerDto
{
    public long PlayerEntityId { get; set; }
    public string Name { get; set; } = "";
    public int Matches { get; set; }
}

/// <summary>Opção do filtro de posição: grupo e quantas linhas jogador×partida o recorte tem nele (sem nenhum filtro aplicado).</summary>
public class AvailablePositionGroupDto
{
    public string PositionGroup { get; set; } = "";
    public int Matches { get; set; }
}

// ----------------------------------------------------------------------------------- GET /api/clubs/{id}/archetypes/summary

public class ArchetypeSummaryTopPlayerDto
{
    public long PlayerEntityId { get; set; }
    public string Name { get; set; } = "";
    public int Matches { get; set; }
    public double? AvgRating { get; set; }
    public double? AvgProOverall { get; set; }
}

public class ArchetypeSummaryRowDto
{
    public ArchetypeRef Archetype { get; set; } = new();
    public int Matches { get; set; }
    public int Players { get; set; }
    public double? AvgRating { get; set; }
    public double? AvgProOverall { get; set; }
    public int? MinProOverall { get; set; }
    public int? MaxProOverall { get; set; }
    public double GoalsPerMatch { get; set; }
    public double AssistsPerMatch { get; set; }
    public double WinPct { get; set; }
    public double? PassAccuracyPct { get; set; }
    public double? ShotAccuracyPct { get; set; }
    public double? TackleAccuracyPct { get; set; }
    public List<ArchetypeSummaryTopPlayerDto> TopPlayers { get; set; } = new();
}

public class ArchetypeSummaryPlayerDto
{
    public long PlayerEntityId { get; set; }
    public string Name { get; set; } = "";
    public int Switches { get; set; }
    public List<ArchetypeUsage> Archetypes { get; set; } = new();

    /// <summary>Total de jogos do jogador no recorte (só para ordenar; não vai no JSON).</summary>
    [JsonIgnore]
    public int Total { get; set; }
}

public class ArchetypeSummaryDto
{
    public long ClubId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public int? GameVersion { get; set; }
    /// <summary>Linhas jogador×partida do recorte (sem desconectados), com ou sem arquétipo.</summary>
    /// <summary>Eco dos filtros (nulos = sem filtro).</summary>
    public string? PositionGroup { get; set; }
    public int? ArchetypeId { get; set; }
    /// <summary>Arquétipos do recorte COM a posição aplicada e SEM o filtro de arquétipo.</summary>
    public List<AvailableArchetypeDto> AvailableArchetypes { get; set; } = new();
    /// <summary>Posições do recorte, sem nenhum dos dois filtros.</summary>
    public List<AvailablePositionGroupDto> AvailablePositionGroups { get; set; } = new();
    public int TotalPlayerMatches { get; set; }
    public int WithoutArchetype { get; set; }
    public List<ArchetypeSummaryRowDto> Archetypes { get; set; } = new();
    public List<ArchetypeSummaryPlayerDto> ByPlayer { get; set; } = new();
}

// ----------------------------------------------------------------------------------- admin

public class AdminArchetypeTopPlayerDto
{
    public long PlayerEntityId { get; set; }
    public string Name { get; set; } = "";
    public int Matches { get; set; }
}

public class AdminArchetypeDto
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? ShortName { get; set; }
    public string? PositionGroup { get; set; }
    public string? InferredPositionGroup { get; set; }
    public int Matches { get; set; }
    public int Players { get; set; }
    public double? AvgProOverall { get; set; }
    public List<AdminArchetypeTopPlayerDto> TopPlayers { get; set; } = new();
    public DateTime? UpdatedAt { get; set; }
}

public class AdminArchetypeUpdateDto
{
    public string? Name { get; set; }
    public string? ShortName { get; set; }
    public string? PositionGroup { get; set; }
}
