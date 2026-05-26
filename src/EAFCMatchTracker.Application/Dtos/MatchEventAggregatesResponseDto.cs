namespace EAFCMatchTracker.Application.Dtos;

public class MatchEventAggregatesResponseDto
{
    /// <summary>Categorias na ordem de exibição das abas (ex.: "Resumo", "Ataque", ...).</summary>
    public List<string> Categories { get; set; } = new();

    /// <summary>Mapeamento completo de IDs de evento para label e categoria.</summary>
    public List<EventDefinitionDto> EventDefinitions { get; set; } = new();

    /// <summary>Dados por clube, com estatísticas individuais de cada jogador.</summary>
    public List<ClubEventAggregatesDto> Clubs { get; set; } = new();
}
