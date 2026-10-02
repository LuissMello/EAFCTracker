using EAFCMatchTracker.Application.Interfaces.Repositories;
using EAFCMatchTracker.Domain.Entities;
using EAFCMatchTracker.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EAFCMatchTracker.Application.Repositories;

public class GoalRegistrationRepository : IGoalRegistrationRepository
{
    private readonly EAFCContext _db;

    public GoalRegistrationRepository(EAFCContext db)
    {
        _db = db;
    }

    public Task<GoalRegistrationEntity?> GetWithGoalsAsync(long id, CancellationToken ct) =>
        _db.GoalRegistrations.Include(r => r.Goals).FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<List<GoalRegistrationEntity>> ListAsync(
        long? clubId, IReadOnlyCollection<GoalRegistrationStatus> statuses, DateTime? createdFromUtc, int limit, CancellationToken ct)
    {
        var q = _db.GoalRegistrations.AsNoTracking().Include(r => r.Goals).Where(r => statuses.Contains(r.Status));
        if (clubId.HasValue) q = q.Where(r => r.ClubId == clubId.Value);
        if (createdFromUtc.HasValue) q = q.Where(r => r.CreatedAt >= createdFromUtc.Value);
        return q.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id).Take(limit).ToListAsync(ct);
    }

    public Task<GoalRegistrationEntity?> GetCurrentAsync(long clubId, DateTime createdFromUtc, CancellationToken ct) =>
        _db.GoalRegistrations.AsNoTracking().Include(r => r.Goals)
            .Where(r => r.ClubId == clubId
                        && (r.Status == GoalRegistrationStatus.Pending || r.Status == GoalRegistrationStatus.NeedsReview)
                        && r.CreatedAt >= createdFromUtc)
            .OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)
            .FirstOrDefaultAsync(ct);

    public void Add(GoalRegistrationEntity registration) => _db.GoalRegistrations.Add(registration);

    public void Remove(GoalRegistrationEntity registration) => _db.GoalRegistrations.Remove(registration);

    public void RemoveGoal(GoalRegistrationGoalEntity goal) => _db.GoalRegistrationGoals.Remove(goal);

    public Task<MatchEntity?> GetMatchAsync(long matchId, CancellationToken ct) =>
        _db.Matches.FirstOrDefaultAsync(m => m.MatchId == matchId, ct);

    public Task<List<MatchGoalLinkEntity>> GetOwnLinksAsync(long registrationId, CancellationToken ct) =>
        _db.MatchGoalLinks.Where(l => l.GoalRegistrationId == registrationId).ToListAsync(ct);

    public void RemoveLinks(IEnumerable<MatchGoalLinkEntity> links) => _db.MatchGoalLinks.RemoveRange(links);

    public async Task<List<KnownPlayerRow>> GetPlayersAsync(IReadOnlyCollection<long> playerEntityIds, CancellationToken ct)
    {
        var ids = playerEntityIds.Distinct().ToList();
        if (ids.Count == 0) return new List<KnownPlayerRow>();
        return await _db.Players.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Select(p => new KnownPlayerRow(p.Id, p.ClubId, p.Playername))
            .ToListAsync(ct);
    }

    public async Task<Dictionary<long, string>> GetDisplayNamesAsync(IReadOnlyCollection<long> playerEntityIds, CancellationToken ct)
    {
        var ids = playerEntityIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<long, string>();

        var rows = await _db.Players.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Select(p => new
            {
                p.Id,
                p.Playername,
                ProName = p.MatchPlayers
                    .Where(mp => mp.ProName != null && mp.ProName != "")
                    .OrderByDescending(mp => mp.MatchId)
                    .Select(mp => mp.ProName)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        return rows.ToDictionary(
            r => r.Id,
            r => !string.IsNullOrWhiteSpace(r.ProName) ? r.ProName! : (r.Playername ?? $"#{r.Id}"));
    }

    public async Task<List<RosterRow>> GetRosterRowsAsync(long clubId, CancellationToken ct)
    {
        var rows = await _db.MatchPlayers.AsNoTracking()
            .Where(mp => mp.ClubId == clubId)
            .GroupBy(mp => mp.PlayerEntityId)
            .Select(g => new { Id = g.Key, Played = g.Count(), Last = g.Max(x => x.Match.Timestamp) })
            .ToListAsync(ct);
        return rows.Select(r => new RosterRow(r.Id, r.Played, r.Last)).ToList();
    }

    public async Task<Dictionary<long, string>> GetLastPositionsAsync(long clubId, IReadOnlyCollection<long> playerEntityIds, CancellationToken ct)
    {
        var ids = playerEntityIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<long, string>();

        var rows = await _db.Players.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Select(p => new
            {
                p.Id,
                Pos = p.MatchPlayers
                    .Where(mp => mp.ClubId == clubId && mp.Pos != null && mp.Pos != "")
                    .OrderByDescending(mp => mp.MatchId)
                    .Select(mp => mp.Pos)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        return rows.Where(r => !string.IsNullOrWhiteSpace(r.Pos)).ToDictionary(r => r.Id, r => r.Pos!);
    }

    public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
