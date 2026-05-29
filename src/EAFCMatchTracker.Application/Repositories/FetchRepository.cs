using EAFCMatchTracker.Application.Interfaces.Repositories;
using EAFCMatchTracker.Domain.Entities;
using EAFCMatchTracker.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EAFCMatchTracker.Application.Repositories;

public class FetchRepository : IFetchRepository
{
    private readonly EAFCContext _db;

    public FetchRepository(EAFCContext db)
    {
        _db = db;
    }

    public async Task<SystemFetchAudit?> GetAuditReadOnlyAsync(CancellationToken ct)
    {
        return await _db.SystemFetchAudits
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == 1, ct);
    }

    public async Task UpsertAuditAsync(DateTimeOffset lastFetchedAt, CancellationToken ct)
    {
        var row = await _db.SystemFetchAudits.SingleOrDefaultAsync(x => x.Id == 1, ct);
        if (row == null)
        {
            row = new SystemFetchAudit { Id = 1, LastFetchedAt = lastFetchedAt };
            _db.SystemFetchAudits.Add(row);
        }
        else
        {
            row.LastFetchedAt = lastFetchedAt;
        }

        await _db.SaveChangesAsync(ct);
    }
}
