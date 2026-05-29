using EAFCMatchTracker.Application.Dtos;

namespace EAFCMatchTracker.Application.Interfaces.Services;

public interface IGoalAnalysisService
{
    Task<GoalAnalysisResponseDto> GetGoalAnalysisAsync(long clubId, DateTime fromUtc, DateTime toUtc, CancellationToken ct);
    Task<MatchGoalsResponseDto?> GetGoalsByMatchIdAsync(long matchId, CancellationToken ct);
    Task RegisterGoalsAsync(long matchId, RegisterGoalsRequest request, CancellationToken ct);
}
