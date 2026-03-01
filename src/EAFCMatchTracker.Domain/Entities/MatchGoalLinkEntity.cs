namespace EAFCMatchTracker.Domain.Entities;

public class MatchGoalLinkEntity
{
    public long Id { get; set; }

    public long MatchId { get; set; }
    public long ClubId { get; set; }

    // FK para PlayerEntity
    public long ScorerPlayerEntityId { get; set; }
    public long? AssistPlayerEntityId { get; set; }
    public long? PreAssistPlayerEntityId { get; set; }

    // Navegação
    public MatchEntity Match { get; set; } = default!;
    public PlayerEntity Scorer { get; set; } = default!;
    public PlayerEntity? Assist { get; set; }
    public PlayerEntity? PreAssist { get; set; }
}
