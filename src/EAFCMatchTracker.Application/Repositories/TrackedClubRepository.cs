using EAFCMatchTracker.Application.Interfaces.Repositories;
using EAFCMatchTracker.Domain.Entities;
using EAFCMatchTracker.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EAFCMatchTracker.Application.Repositories;

public class TrackedClubRepository : ITrackedClubRepository
{
    private readonly EAFCContext _db;

    public TrackedClubRepository(EAFCContext db)
    {
        _db = db;
    }

    public Task<List<TrackedClubEntity>> GetAllAsync(CancellationToken ct) =>
        _db.TrackedClubs.AsNoTracking().OrderBy(c => c.ClubId).ToListAsync(ct);

    public Task<TrackedClubEntity?> GetByIdAsync(long clubId, CancellationToken ct) =>
        _db.TrackedClubs.AsNoTracking().FirstOrDefaultAsync(c => c.ClubId == clubId, ct);

    public async Task<(TrackedClubEntity Club, bool Created)> AddAsync(TrackedClubEntity club, CancellationToken ct)
    {
        var existing = await _db.TrackedClubs.AsNoTracking().FirstOrDefaultAsync(c => c.ClubId == club.ClubId, ct);
        if (existing is not null)
            return (existing, false);

        _db.TrackedClubs.Add(club);
        await _db.SaveChangesAsync(ct);
        return (club, true);
    }

    public async Task<TrackedClubEntity?> SetGameVersionAsync(long clubId, int? gameVersionId, CancellationToken ct)
    {
        var entity = await _db.TrackedClubs.FirstOrDefaultAsync(c => c.ClubId == clubId, ct);
        if (entity is null) return null;

        entity.GameVersionId = gameVersionId;
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<bool> RemoveAsync(long clubId, CancellationToken ct)
    {
        var entity = await _db.TrackedClubs.FindAsync([clubId], ct);
        if (entity is null) return false;

        _db.TrackedClubs.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
