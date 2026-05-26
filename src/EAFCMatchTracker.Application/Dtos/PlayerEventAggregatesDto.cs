namespace EAFCMatchTracker.Application.Dtos;

public class PlayerEventAggregatesDto
{
    public long PlayerId { get; set; }
    public string PlayerName { get; set; } = default!;

    /// <summary>
    /// Chave = ID do evento (string numérica), Valor = total de ocorrências para este jogador.
    /// </summary>
    public Dictionary<string, int> Events { get; set; } = new();
}
