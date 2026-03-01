namespace EAFCMatchTracker.Application.Dtos;

public class MatchGoalItemDto
{
    public long MatchId { get; set; }
    public long ClubId { get; set; }

    public long ScorerPlayerEntityId { get; set; }
    public string ScorerName { get; set; }

    public long? AssistPlayerEntityId { get; set; }
    public string AssistName { get; set; }

    public long? PreAssistPlayerEntityId { get; set; }
    public string PreAssistName { get; set; }
}
