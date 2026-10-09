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

    /// <summary>
    /// UTC. O usuário encerrou o registro ("Finalizar"): ele deixa de ser o registro em andamento (GET current) e passa a
    /// ser elegível à sugestão de partida do linker. Pode ser desfeito (reopen) enquanto Pending.
    /// </summary>
    public DateTime? FinishedAt { get; set; }

    /// <summary>
    /// Partida SUGERIDA pelo linker (outro adversário, mesmo nº de gols, dentro da janela) para confirmação humana.
    /// Sem FK de propósito: se a partida for apagada, o linker limpa a sugestão. Nunca vincula sozinho.
    /// </summary>
    public long? SuggestedMatchId { get; set; }

    /// <summary>Última sugestão recusada (dismiss): o linker não volta a sugerir a mesma partida.</summary>
    public long? DismissedMatchId { get; set; }

    public ICollection<GoalRegistrationGoalEntity> Goals { get; set; } = new List<GoalRegistrationGoalEntity>();
}
