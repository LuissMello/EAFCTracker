using EAFCMatchTracker.Application.Interfaces.Repositories;
using EAFCMatchTracker.Domain.Entities;
using EAFCMatchTracker.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EAFCMatchTracker.Application.Repositories;

public class GameVersionRepository : IGameVersionRepository
{
    private readonly EAFCContext _db;

    public GameVersionRepository(EAFCContext db)
    {
        _db = db;
    }

    public Task<List<GameVersionEntity>> GetAllAsync(CancellationToken ct) =>
        _db.GameVersions.AsNoTracking().OrderBy(v => v.Version).ToListAsync(ct);

    public Task<GameVersionEntity?> GetByVersionAsync(int version, CancellationToken ct) =>
        _db.GameVersions.AsNoTracking().FirstOrDefaultAsync(v => v.Version == version, ct);

    public Task<GameVersionEntity?> GetCurrentAsync(CancellationToken ct) =>
        _db.GameVersions.AsNoTracking().FirstOrDefaultAsync(v => v.IsCurrent, ct);

    public async Task<(GameVersionEntity? Entity, bool Created)> CreateAsync(
        int version, string name, DateTimeOffset? startsAt, CancellationToken ct)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync<(GameVersionEntity?, bool)>(async () =>
            {
                await using var tx = await _db.Database.BeginTransactionAsync(ct);

                if (await _db.GameVersions.AnyAsync(v => v.Version == version, ct))
                    return ((GameVersionEntity?)null, false);

                var nextId = (await _db.GameVersions.MaxAsync(v => (int?)v.Id, ct) ?? 0) + 1;
                var entity = new GameVersionEntity
                {
                    Id = nextId,
                    Version = version,
                    Name = name,
                    StartsAt = startsAt,
                    IsCurrent = false
                };

                _db.GameVersions.Add(entity);
                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return ((GameVersionEntity?)entity, true);
            });
        }
        catch (DbUpdateException)
        {
            // Corrida com outra criação da mesma edição (índice único em Version)?
            _db.ChangeTracker.Clear();
            if (await _db.GameVersions.AsNoTracking().AnyAsync(v => v.Version == version, CancellationToken.None))
                return (null, false);
            throw;
        }
    }

    public async Task<GameVersionEntity?> SetCurrentAsync(int version, CancellationToken ct)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync<GameVersionEntity?>(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            var target = await _db.GameVersions.FirstOrDefaultAsync(v => v.Version == version, ct);
            if (target is null) return null;

            // Duas etapas (desmarca -> marca): o índice único filtrado em IsCurrent não admite dois "true" nem
            // por instantes, e o EF não garante a ordem dos UPDATEs dentro de um mesmo SaveChanges.
            var others = await _db.GameVersions.Where(v => v.IsCurrent && v.Id != target.Id).ToListAsync(ct);
            if (others.Count > 0)
            {
                foreach (var o in others) o.IsCurrent = false;
                await _db.SaveChangesAsync(ct);
            }

            if (!target.IsCurrent)
            {
                target.IsCurrent = true;
                await _db.SaveChangesAsync(ct);
            }

            await tx.CommitAsync(ct);
            return target;
        });
    }
}
