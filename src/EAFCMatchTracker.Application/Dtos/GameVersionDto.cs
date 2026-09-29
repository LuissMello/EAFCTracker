using EAFCMatchTracker.Domain.Entities;

namespace EAFCMatchTracker.Application.Dtos;

/// <summary>Edição do jogo: { id, version, name, startsAt, isCurrent }.</summary>
public sealed record GameVersionDto(int Id, int Version, string Name, DateTimeOffset? StartsAt, bool IsCurrent)
{
    public static GameVersionDto From(GameVersionEntity e) => new(e.Id, e.Version, e.Name, e.StartsAt, e.IsCurrent);
}
