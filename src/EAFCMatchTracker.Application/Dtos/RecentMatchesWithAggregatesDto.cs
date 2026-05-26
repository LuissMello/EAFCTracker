namespace EAFCMatchTracker.Application.Dtos;

/// <summary>
/// Resposta do endpoint GET /api/Maintenance/matches/recent-aggregates.
/// Inclui as definições de evento (únicas, sem repetição) e as N partidas
/// mais recentes com TODOS os seus dados (placar, jogadores, estatísticas e
/// event-aggregates individuais).
/// </summary>
public class RecentMatchesWithAggregatesDto
{
    /// <summary>Quantidade solicitada pelo chamador.</summary>
    public int RequestedCount { get; set; }

    /// <summary>Quantidade efetivamente retornada (pode ser menor se não houver partidas suficientes).</summary>
    public int ReturnedCount { get; set; }

    /// <summary>Categorias de eventos na ordem de exibição das abas.</summary>
    public List<string> Categories { get; set; } = new();

    /// <summary>Mapeamento completo de IDs de evento para label e categoria.</summary>
    public List<EventDefinitionDto> EventDefinitions { get; set; } = new();

    /// <summary>Partidas em ordem decrescente de data (mais recente primeiro), com todos os dados.</summary>
    public List<FullMatchDataDto> Matches { get; set; } = new();
}
