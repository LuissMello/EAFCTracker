namespace EAFCMatchTracker.Application.Dtos;

public sealed class ClubListItemDto
{
    public long ClubId { get; init; }
    public string Name { get; init; } = default!;
    public string? CrestAssetId { get; init; }
}
