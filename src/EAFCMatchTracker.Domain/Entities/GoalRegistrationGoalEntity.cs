namespace EAFCMatchTracker.Domain.Entities;

/// <summary>Uma linha (gol) de um registro antecipado. Ordem preservada em <see cref="Order"/>.</summary>
public class GoalRegistrationGoalEntity
{
    public long Id { get; set; }
    public long GoalRegistrationId { get; set; }
    public int Order { get; set; }

    // FK para PlayerEntity (Players.Id), como em MatchGoalLinkEntity
    public long ScorerPlayerEntityId { get; set; }
    public long? AssistPlayerEntityId { get; set; }
    public long? PreAssistPlayerEntityId { get; set; }

    public GoalRegistrationEntity GoalRegistration { get; set; } = default!;
    public PlayerEntity Scorer { get; set; } = default!;
    public PlayerEntity? Assist { get; set; }
    public PlayerEntity? PreAssist { get; set; }
}
