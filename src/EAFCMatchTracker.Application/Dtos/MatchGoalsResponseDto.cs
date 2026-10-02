namespace EAFCMatchTracker.Application.Dtos;

public class MatchGoalsResponseDto
{
    public long MatchId { get; set; }

    /// <summary>Registro de gols (GoalRegistrations) vinculado a esta partida, se houver.</summary>
    public long? GoalRegistrationId { get; set; }

    /// <summary>"Pending" | "Linked" | "NeedsReview" | "Expired" do registro vinculado (null sem registro).</summary>
    public string? RegistrationStatus { get; set; }

    /// <summary>Observação de revisão do registro vinculado (null sem registro/observação).</summary>
    public string? ReviewNote { get; set; }

    public int TotalGoals { get; set; }
    public List<MatchGoalItemDto> Goals { get; set; } = new();
}
