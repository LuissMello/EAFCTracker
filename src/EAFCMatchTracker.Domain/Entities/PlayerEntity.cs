using System.ComponentModel.DataAnnotations;

namespace EAFCMatchTracker.Domain.Entities;

public class PlayerEntity
{
    [Key]
    public long Id { get; set; }
    public long PlayerId { get; set; }
    public long ClubId { get; set; }
    public string Playername { get; set; }

    public PlayerMatchStatsEntity PlayerMatchStats { get; set; }
    public long? PlayerMatchStatsId { get; set; }

    public ICollection<MatchPlayerEntity> MatchPlayers { get; set; } = new List<MatchPlayerEntity>();
}
