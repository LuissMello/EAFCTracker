using EAFCMatchTracker.Application.Interfaces.Repositories;
using EAFCMatchTracker.Domain.Entities;
using EAFCMatchTracker.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EAFCMatchTracker.Application.Repositories;

public class GoalRepository : IGoalRepository
{
    private readonly EAFCContext _db;

    public GoalRepository(EAFCContext db)
    {
        _db = db;
    }

    public async Task<List<MatchGoalLinkEntity>> GetGoalLinksByMatchIdsAsync(List<long> matchIds, long clubId, CancellationToken ct)
    {
        return await _db.MatchGoalLinks
            .AsNoTracking()
            .Where(g => matchIds.Contains(g.MatchId) && g.ClubId == clubId)
            .ToListAsync(ct);
    }

    public async Task<List<MatchGoalLinkEntity>> GetGoalLinksByMatchIdAsync(long matchId, CancellationToken ct)
    {
        return await _db.MatchGoalLinks
            .Where(g => g.MatchId == matchId)
            .ToListAsync(ct);
    }

    public Task AddGoalLinkAsync(MatchGoalLinkEntity entity, CancellationToken ct)
    {
        _db.MatchGoalLinks.Add(entity);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        await _db.SaveChangesAsync(ct);
    }
}
