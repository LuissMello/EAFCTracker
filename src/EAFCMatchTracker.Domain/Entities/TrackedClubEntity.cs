namespace EAFCMatchTracker.Domain.Entities;

public class TrackedClubEntity
{
    public long ClubId { get; set; }
    public string? Name { get; set; }
    public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Edição do jogo em que este clube é rastreado (FK GameVersions).</summary>
    public int? GameVersionId { get; set; }

    public string TimeZoneId { get; set; } = "America/Sao_Paulo";
    public int SessionGapMinutes { get; set; } = 120;
}
