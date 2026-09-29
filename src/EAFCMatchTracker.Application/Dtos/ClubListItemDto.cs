namespace EAFCMatchTracker.Application.Dtos;

public sealed class ClubListItemDto
{
    public long ClubId { get; init; }
    public string Name { get; init; } = default!;
    public string? CrestAssetId { get; init; }

    /// <summary>Edição do jogo em que o clube é rastreado (ex.: 27). Nulo se não definida.</summary>
    public int? GameVersion { get; init; }
}
