namespace EAFCMatchTracker.Application.Dtos;

public sealed class PlayerAttributeSnapshotDto
{
    public long PlayerId { get; set; }
    public string PlayerName { get; set; } = "";
    public long ClubId { get; set; }
    public string Pos { get; set; }
    public PlayerMatchStatsDto? Statistics { get; set; }

    /// <summary>Arquétipo da partida de onde vêm estes atributos (a mais recente do jogador); 0 = sem dado.</summary>
    public short ArchetypeId { get; set; }
    public ArchetypeRef? Archetype { get; set; }
}
