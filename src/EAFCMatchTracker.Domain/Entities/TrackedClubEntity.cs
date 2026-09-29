namespace EAFCMatchTracker.Domain.Entities;

public class TrackedClubEntity
{
    public long ClubId { get; set; }
    public string? Name { get; set; }
    public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Edição do jogo em que este clube é rastreado (FK GameVersions).</summary>
    public int? GameVersionId { get; set; }
}
