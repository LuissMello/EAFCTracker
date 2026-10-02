namespace EAFCMatchTracker.Domain.Entities;

/// <summary>
/// Cabeçalho de um registro antecipado de gols: o que o jogador anotou no caderno sobre UMA partida
/// (adversário + gols com autor/assistência/pré-assistência). É vinculado à partida real quando ela é buscada.
/// </summary>
public class GoalRegistrationEntity
{
    public long Id { get; set; }

    /// <summary>Nosso clube (autor dos gols registrados).</summary>
    public long ClubId { get; set; }

    public long OpponentClubId { get; set; }

    /// <summary>Nome do adversário no momento do registro (snapshot).</summary>
    public string OpponentName { get; set; } = string.Empty;

    public string? Notes { get; set; }

    /// <summary>UTC.</summary>
    public DateTime CreatedAt { get; set; }

    public GoalRegistrationStatus Status { get; set; } = GoalRegistrationStatus.Pending;

    /// <summary>Partida vinculada (nula enquanto Pending/Expired ou se a partida foi apagada).</summary>
    public long? MatchId { get; set; }

    /// <summary>UTC. Preenchido apenas quando Linked.</summary>
    public DateTime? LinkedAt { get; set; }

    public string? ReviewNote { get; set; }

    public ICollection<GoalRegistrationGoalEntity> Goals { get; set; } = new List<GoalRegistrationGoalEntity>();
}
