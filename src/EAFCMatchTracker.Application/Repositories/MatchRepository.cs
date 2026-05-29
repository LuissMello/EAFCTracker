using EAFCMatchTracker.Application.Interfaces.Repositories;
using EAFCMatchTracker.Domain.Entities;
using EAFCMatchTracker.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EAFCMatchTracker.Application.Repositories;

public class MatchRepository : IMatchRepository
{
    private readonly EAFCContext _db;

    public MatchRepository(EAFCContext db)
    {
        _db = db;
    }

    public IQueryable<MatchEntity> QueryMatchesByClubId(long clubId)
    {
        return _db.Matches
            .AsNoTracking()
            .Include(m => m.Clubs.Where(c => c.ClubId == clubId)).ThenInclude(c => c.Details)
            .Include(m => m.MatchPlayers).ThenInclude(mp => mp.Player)
            .Where(m => m.Clubs.Any(c => c.ClubId == clubId));
    }

    public IQueryable<MatchEntity> QueryMatchesByClubIds(IReadOnlyCollection<long> ids)
    {
        return _db.Matches
            .AsNoTracking()
            .Where(m => m.Clubs.Any(c => ids.Contains(c.ClubId)));
    }

    public async Task<List<MatchEntity>> GetMatchesForClubWithPlayersAsync(long clubId, int count, CancellationToken ct)
    {
        return await _db.Matches
            .AsNoTracking()
            .Where(m => m.Clubs.Any(c => c.ClubId == clubId))
            .Include(m => m.MatchPlayers.Where(mp => mp.ClubId == clubId))
                .ThenInclude(mp => mp.Player)
            .Include(m => m.MatchPlayers.Where(mp => mp.ClubId == clubId))
                .ThenInclude(mp => mp.PlayerMatchStats)
            .OrderByDescending(m => m.Timestamp)
            .Take(count)
            .ToListAsync(ct);
    }

    public async Task<List<MatchEntity>> GetMatchesForClubFullAsync(long clubId, CancellationToken ct)
    {
        return await _db.Matches
            .AsNoTracking()
            .Include(m => m.Clubs).ThenInclude(c => c.Details)
            .Include(m => m.MatchPlayers).ThenInclude(mp => mp.Player)
            .Where(m => m.Clubs.Any(c => c.ClubId == clubId))
            .OrderByDescending(m => m.Timestamp)
            .ToListAsync(ct);
    }

    public async Task<List<MatchEntity>> GetMatchesForClubIdsInDateRangeAsync(
        IReadOnlyCollection<long> ids, DateTime startUtc, DateTime endExclusiveUtc, CancellationToken ct)
    {
        return await _db.Matches
            .AsNoTracking()
            .Include(m => m.Clubs).ThenInclude(c => c.Details)
            .Include(m => m.MatchPlayers).ThenInclude(mp => mp.Player)
            .Where(m => m.Clubs.Any(c => ids.Contains(c.ClubId)))
            .Where(m => m.Timestamp >= startUtc && m.Timestamp < endExclusiveUtc)
            .OrderBy(m => m.Timestamp)
            .ToListAsync(ct);
    }

    public async Task<List<MatchEntity>> GetMatchesForPlayerInClubsInDateRangeAsync(
        long playerId, IReadOnlyCollection<long> clubIds, DateTime startUtc, DateTime endExclusiveUtc, CancellationToken ct)
    {
        return await _db.Matches
            .AsNoTracking()
            .Include(m => m.Clubs).ThenInclude(c => c.Details)
            .Include(m => m.MatchPlayers).ThenInclude(mp => mp.Player)
            .Where(m => m.MatchPlayers.Any(mp => mp.Player.PlayerId == playerId && clubIds.Contains(mp.ClubId)))
            .Where(m => m.Timestamp >= startUtc && m.Timestamp < endExclusiveUtc)
            .OrderBy(m => m.Timestamp)
            .ToListAsync(ct);
    }

    public async Task<int> CountMatchesByQueryAsync(IQueryable<MatchEntity> query, CancellationToken ct)
    {
        return await query.CountAsync(ct);
    }

    public async Task<List<MatchEntity>> GetPagedMatchesAsync(IQueryable<MatchEntity> query, int skip, int take, CancellationToken ct)
    {
        return await query
            .OrderByDescending(m => m.Timestamp)
            .ThenByDescending(m => m.MatchId)
            .Skip(skip)
            .Take(take)
            .Include(m => m.Clubs).ThenInclude(c => c.Details)
            .Include(m => m.MatchPlayers).ThenInclude(mp => mp.Player)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<List<MatchEntity>> GetMatchesByClubIdsAsync(IReadOnlyCollection<long> ids, CancellationToken ct)
    {
        return await _db.Matches
            .AsNoTracking()
            .Include(m => m.Clubs).ThenInclude(c => c.Details)
            .Include(m => m.MatchPlayers).ThenInclude(mp => mp.Player)
            .Where(m => m.Clubs.Any(c => ids.Contains(c.ClubId)))
            .OrderBy(m => m.Timestamp)
            .ToListAsync(ct);
    }

    public async Task<MatchEntity?> GetMatchWithPlayersAndClubsAsync(long matchId, CancellationToken ct)
    {
        return await _db.Matches
            .AsNoTracking()
            .Include(m => m.Clubs).ThenInclude(c => c.Details)
            .Include(m => m.MatchPlayers).ThenInclude(mp => mp.Player)
            .FirstOrDefaultAsync(m => m.MatchId == matchId, ct);
    }

    public async Task<List<long>> GetMatchIdsByClubIdAsync(long clubId, CancellationToken ct)
    {
        return await _db.MatchClubs
            .Where(mc => mc.ClubId == clubId)
            .Select(mc => mc.MatchId)
            .Distinct()
            .ToListAsync(ct);
    }

    public async Task DeleteMatchesAsync(IEnumerable<long> matchIds, CancellationToken ct)
    {
        var ids = matchIds.ToList();
        var strategy = _db.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                var statsIds = await _db.MatchPlayers
                    .Where(mp => ids.Contains(mp.MatchId))
                    .Select(mp => mp.PlayerMatchStatsEntityId)
                    .Where(id => id != null)
                    .Cast<long>()
                    .Distinct()
                    .ToListAsync(ct);

                await _db.PlayerMatchStats.Where(pms => statsIds.Contains(pms.Id)).ExecuteDeleteAsync(ct);
                await _db.MatchPlayers.Where(mp => ids.Contains(mp.MatchId)).ExecuteDeleteAsync(ct);
                await _db.MatchClubs.Where(mc => ids.Contains(mc.MatchId)).ExecuteDeleteAsync(ct);
                await _db.Matches.Where(m => ids.Contains(m.MatchId)).ExecuteDeleteAsync(ct);

                await tx.CommitAsync(ct);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        });
    }

    public async Task<List<MatchEntity>> GetRecentMatchesWithFullDataAsync(int count, CancellationToken ct)
    {
        return await _db.Matches
            .AsNoTracking()
            .OrderByDescending(m => m.Timestamp)
            .Take(count)
            .Include(m => m.Clubs).ThenInclude(c => c.Details)
            .Include(m => m.MatchPlayers).ThenInclude(mp => mp.Player)
            .ToListAsync(ct);
    }

    public async Task<List<MatchEntity>> GetMatchesForTrendsAsync(long clubId, int last, DateTime? since, DateTime? until, CancellationToken ct)
    {
        return await _db.Matches
            .Include(m => m.Clubs).ThenInclude(c => c.Details)
            .Include(m => m.MatchPlayers).ThenInclude(mp => mp.Player)
            .Where(m => m.Clubs.Any(c => c.ClubId == clubId))
            .Where(m => !since.HasValue || m.Timestamp >= since.Value)
            .Where(m => !until.HasValue || m.Timestamp <= until.Value)
            .OrderByDescending(m => m.Timestamp)
            .Take(last > 0 ? last : 30)
            .ToListAsync(ct);
    }
}
