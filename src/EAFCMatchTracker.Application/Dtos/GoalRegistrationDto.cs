namespace EAFCMatchTracker.Application.Dtos;

public class GoalRegistrationDto
{
    public long ScorerPlayerEntityId { get; set; }
    public long? AssistPlayerEntityId { get; set; }
    public long? PreAssistPlayerEntityId { get; set; }
}
