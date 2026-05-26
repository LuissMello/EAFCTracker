namespace EAFCMatchTracker.Application.Dtos;

public class ClubEventAggregatesDto
{
    public long ClubId { get; set; }
    public string ClubName { get; set; } = default!;
    public string? CrestAssetId { get; set; }

    /// <summary>Gols marcados pelo clube nesta partida.</summary>
    public short Goals { get; set; }

    /// <summary>Gols sofridos pelo clube nesta partida.</summary>
    public short GoalsAgainst { get; set; }

    /// <summary>
    /// Estatísticas de eventos individuais de cada jogador do clube nesta partida.
    /// </summary>
    public List<PlayerEventAggregatesDto> Players { get; set; } = new();
}
