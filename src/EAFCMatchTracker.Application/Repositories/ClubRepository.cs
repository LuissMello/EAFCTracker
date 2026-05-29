using EAFCMatchTracker.Application.Interfaces.Repositories;
using EAFCMatchTracker.Domain.Entities;
using EAFCMatchTracker.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EAFCMatchTracker.Application.Repositories;

public class ClubRepository : IClubRepository
{
    private readonly EAFCContext _db;

    public ClubRepository(EAFCContext db)
    {
        _db = db;
    }

    public async Task<List<(long ClubId, string? Name, int Team)>> GetClubSummariesByIdsAsync(List<long> ids, CancellationToken ct)
    {
        var raw = await _db.MatchClubs
            .AsNoTracking()
            .Where(mc => ids.Contains(mc.ClubId))
            .GroupBy(mc => mc.ClubId)
            .Select(g => new
            {
                ClubId = g.Key,
                Name = g.OrderByDescending(x => x.Match.Timestamp)
                         .Select(x => x.Details.Name)
                         .FirstOrDefault(),
                Team = g.OrderByDescending(x => x.Match.Timestamp)
                         .Select(x => x.Team)
                         .FirstOrDefault()
            })
            .OrderBy(x => x.Name)
            .ToListAsync(ct);

        return raw.Select(r => (r.ClubId, r.Name, r.Team)).ToList();
    }

    public async Task<List<MatchClubEntity>> GetMatchClubsByIdsAsync(List<long> ids, CancellationToken ct)
    {
        return await _db.MatchClubs
            .AsNoTracking()
            .Where(mc => ids.Contains(mc.ClubId))
            .ToListAsync(ct);
    }

    public async Task<bool> ExistsAsync(long clubId, CancellationToken ct)
    {
        return await _db.MatchClubs.AnyAsync(c => c.ClubId == clubId, ct);
    }

    public async Task<OverallStatsEntity?> GetOverallStatsByClubIdAsync(long clubId, CancellationToken ct)
    {
        return await _db.OverallStats.FirstOrDefaultAsync(o => o.ClubId == clubId, ct);
    }

    public async Task<List<OverallStatsEntity>> GetAllOverallStatsByClubIdAsync(long clubId, CancellationToken ct)
    {
        return await _db.OverallStats.Where(o => o.ClubId == clubId).ToListAsync(ct);
    }

    public async Task<List<OverallStatsEntity>> GetOverallStatsByClubIdsAsync(List<long> ids, CancellationToken ct)
    {
        return await _db.OverallStats
            .AsNoTracking()
            .Where(o => ids.Contains(o.ClubId))
            .ToListAsync(ct);
    }

    public async Task<List<PlayoffAchievementEntity>> GetPlayoffAchievementsByClubIdAsync(long clubId, CancellationToken ct)
    {
        return await _db.PlayoffAchievements
            .AsNoTracking()
            .Where(p => p.ClubId == clubId)
            .ToListAsync(ct);
    }

    public async Task<List<long>> GetAllDistinctClubIdsAsync(CancellationToken ct)
    {
        return await _db.MatchClubs.AsNoTracking().Select(mc => mc.ClubId).Distinct().ToListAsync(ct);
    }

    public async Task<string?> GetLatestClubNameAsync(long clubId, CancellationToken ct)
    {
        return await _db.MatchClubs
            .AsNoTracking()
            .Where(mc => mc.ClubId == clubId && mc.Details != null && mc.Details.Name != null)
            .OrderByDescending(mc => mc.Id)
            .Select(mc => mc.Details!.Name!)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<List<PlayoffAchievementEntity>> GetPlayoffAchievementsForUpdateAsync(long clubId, CancellationToken ct)
    {
        return await _db.PlayoffAchievements.Where(p => p.ClubId == clubId).ToListAsync(ct);
    }

    public async Task AddOverallStatsAsync(OverallStatsEntity entity, CancellationToken ct)
    {
        await _db.OverallStats.AddAsync(entity, ct);
    }

    public Task UpdateOverallStatsAsync(OverallStatsEntity entity)
    {
        _db.OverallStats.Update(entity);
        return Task.CompletedTask;
    }

    public Task UpdateOverallStatsRangeAsync(IEnumerable<OverallStatsEntity> entities)
    {
        _db.OverallStats.UpdateRange(entities);
        return Task.CompletedTask;
    }

    public async Task AddPlayoffAchievementAsync(PlayoffAchievementEntity entity, CancellationToken ct)
    {
        await _db.PlayoffAchievements.AddAsync(entity, ct);
    }

    public Task UpdatePlayoffAchievementAsync(PlayoffAchievementEntity entity)
    {
        _db.PlayoffAchievements.Update(entity);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        await _db.SaveChangesAsync(ct);
    }
}
