using EAFCMatchTracker.Application.Interfaces.Repositories;
using EAFCMatchTracker.Domain.Entities;
using EAFCMatchTracker.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EAFCMatchTracker.Application.Repositories;

public class PlayerRepository : IPlayerRepository
{
    private readonly EAFCContext _db;

    public PlayerRepository(EAFCContext db)
    {
        _db = db;
    }

    public async Task<PlayerEntity?> GetByPlayerIdAsync(long playerId, CancellationToken ct)
    {
        return await _db.Players.AsNoTracking().FirstOrDefaultAsync(p => p.PlayerId == playerId, ct);
    }

    public async Task<List<MatchPlayerEntity>> GetMatchPlayersForClubAsync(long clubId, CancellationToken ct)
    {
        return await _db.MatchPlayers
            .Include(mp => mp.Player)
            .Where(mp => mp.ClubId == clubId && mp.Player != null)
            .ToListAsync(ct);
    }

    public async Task<List<MatchPlayerEntity>> GetMatchPlayersForClubInMatchesAsync(long clubId, List<long> matchIds, CancellationToken ct)
    {
        return await _db.MatchPlayers
            .AsNoTracking()
            .Include(mp => mp.Player)
            .Include(mp => mp.Match)
            .Where(p => matchIds.Contains(p.MatchId) && p.ClubId == clubId)
            .ToListAsync(ct);
    }

    public async Task<List<MatchPlayerEntity>> GetMatchPlayersByEntityIdAsync(long playerEntityId, CancellationToken ct)
    {
        return await _db.MatchPlayers
            .AsNoTracking()
            .Include(mp => mp.Player)
            .Include(mp => mp.Match).ThenInclude(m => m.Clubs).ThenInclude(c => c.Details)
            .Where(mp => mp.PlayerEntityId == playerEntityId)
            .OrderBy(mp => mp.Match.Timestamp)
            .ToListAsync(ct);
    }

    public async Task<Dictionary<long, int?>> GetFallbackDivisionsAsync(List<long> clubIds, CancellationToken ct)
    {
        if (clubIds.Count == 0)
            return new Dictionary<long, int?>();

        var stats = await _db.OverallStats
            .AsNoTracking()
            .Where(os => clubIds.Contains(os.ClubId))
            .Select(os => new { os.ClubId, os.CurrentDivision })
            .ToListAsync(ct);

        return stats
            .GroupBy(x => x.ClubId)
            .ToDictionary(g => g.Key, g => (int?)g.First().CurrentDivision);
    }

    public Task UpdateMatchPlayersRangeAsync(IEnumerable<MatchPlayerEntity> players)
    {
        // EF change tracking handles this
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        await _db.SaveChangesAsync(ct);
    }
}
