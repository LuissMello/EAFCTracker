using System.ComponentModel.DataAnnotations;

namespace EAFCMatchTracker.Domain.Entities;

public class MatchEntity
{
    [Key]
    public long MatchId { get; set; }
    public DateTime Timestamp { get; set; }
    public MatchType MatchType { get; set; }

    /// <summary>Edição do jogo (FK GameVersions). Nulo apenas se nenhuma edição existia na gravação.</summary>
    public int? GameVersionId { get; set; }

    /// <summary>Registro antecipado de gols (GoalRegistrations) vinculado a esta partida, se houver.</summary>
    public long? GoalRegistrationId { get; set; }

    public ICollection<MatchClubEntity> Clubs { get; set; }
    public ICollection<MatchPlayerEntity> MatchPlayers { get; set; }
}
