namespace EAFCMatchTracker.Application.Dtos;

/// <summary>
/// Todos os dados de uma partida em um único objeto:
/// informações básicas, clubes, jogadores, estatísticas agregadas
/// e event-aggregates individuais (EA FC post-game).
/// </summary>
public class FullMatchDataDto
{
    /// <summary>Identificador único da partida.</summary>
    public long MatchId { get; set; }

    /// <summary>Data/hora da partida.</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>Tipo da partida (ex.: "LeagueMatch").</summary>
    public string MatchType { get; set; } = default!;

    /// <summary>
    /// Clubes com placar e dados completos (nome, brasão, cores de kit, etc.).
    /// </summary>
    public List<MatchClubDto> Clubs { get; set; } = new();

    /// <summary>
    /// Jogadores com todas as estatísticas individuais da partida
    /// (gols, assistências, nota, passe, etc.).
    /// </summary>
    public List<MatchPlayerDto> Players { get; set; } = new();

    /// <summary>
    /// Estatísticas agregadas da partida:
    /// visão geral, por jogador e por clube.
    /// </summary>
    public MatchStatisticsResponseDto Statistics { get; set; } = default!;

    /// <summary>
    /// Event-aggregates individuais no estilo EA FC post-game,
    /// agrupados por clube → jogadores → dicionário de eventos.
    /// </summary>
    public List<ClubEventAggregatesDto> EventAggregates { get; set; } = new();
}
