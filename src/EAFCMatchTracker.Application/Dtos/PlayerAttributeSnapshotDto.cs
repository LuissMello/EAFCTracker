namespace EAFCMatchTracker.Application.Dtos;

public sealed class PlayerAttributeSnapshotDto
{
    public long PlayerId { get; set; }
    public string PlayerName { get; set; } = "";
    public long ClubId { get; set; }
    public string Pos { get; set; }
    public PlayerMatchStatsDto? Statistics { get; set; }
}
