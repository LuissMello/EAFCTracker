using System.ComponentModel.DataAnnotations;

namespace EAFCMatchTracker.Domain.Entities;

public class MatchEntity
{
    [Key]
    public long MatchId { get; set; }
    public DateTime Timestamp { get; set; }
    public MatchType MatchType { get; set; }
    public ICollection<MatchClubEntity> Clubs { get; set; }
    public ICollection<MatchPlayerEntity> MatchPlayers { get; set; }
}
