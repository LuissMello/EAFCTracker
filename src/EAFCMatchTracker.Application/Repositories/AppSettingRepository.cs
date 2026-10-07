using EAFCMatchTracker.Application.Interfaces.Repositories;
using EAFCMatchTracker.Application.Interfaces.Services;
using EAFCMatchTracker.Domain.Entities;
using EAFCMatchTracker.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EAFCMatchTracker.Application.Repositories;

public class AppSettingRepository : IAppSettingRepository
{
    private readonly EAFCContext _db;
    private readonly ILiveModeService? _live;

    /// <param name="live">Opcional: recebe a invalidação do cache do agendamento a cada escrita (ponto único de escrita de AppSettings).</param>
    public AppSettingRepository(EAFCContext db, ILiveModeService? live = null)
    {
        _db = db;
        _live = live;
    }

    public Task<List<AppSettingEntity>> GetAllAsync(CancellationToken ct) =>
        _db.AppSettings.AsNoTracking().ToListAsync(ct);

    public Task<AppSettingEntity?> GetByKeyAsync(string key, CancellationToken ct) =>
        _db.AppSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == key, ct);

    public async Task UpsertAsync(string key, string value, CancellationToken ct)
    {
        var existing = await _db.AppSettings.FindAsync([key], ct);
        if (existing is null)
            _db.AppSettings.Add(new AppSettingEntity { Key = key, Value = value });
        else
            existing.Value = value;

        await _db.SaveChangesAsync(ct);
        _live?.Invalidate(); // síncrono: o cache do agendamento nunca fica velho para quem acabou de gravar
    }
}
