namespace EAFCMatchTracker.Domain.Entities;

/// <summary>
/// Edição do jogo (ex.: FC25, FC26, FC27). Clubes rastreados, partidas, overall stats e playoffs
/// pertencem a uma edição. Apenas UMA edição pode ter IsCurrent = true.
/// </summary>
public class GameVersionEntity
{
    public int Id { get; set; }

    /// <summary>Número da edição (25, 26, 27...). Único.</summary>
    public int Version { get; set; }

    public string Name { get; set; } = "";

    /// <summary>Data de lançamento/início da edição (opcional).</summary>
    public DateTimeOffset? StartsAt { get; set; }

    public bool IsCurrent { get; set; }
}
