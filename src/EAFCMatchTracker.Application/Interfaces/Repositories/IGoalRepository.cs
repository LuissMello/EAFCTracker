using EAFCMatchTracker.Domain.Entities;

namespace EAFCMatchTracker.Application.Interfaces.Repositories;

public interface IGoalRepository
{
    Task<List<MatchGoalLinkEntity>> GetGoalLinksByMatchIdsAsync(List<long> matchIds, long clubId, CancellationToken ct);
    Task<List<MatchGoalLinkEntity>> GetGoalLinksByMatchIdAsync(long matchId, CancellationToken ct);
    Task AddGoalLinkAsync(MatchGoalLinkEntity entity, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
