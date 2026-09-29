using EAFCMatchTracker.Domain.Entities;

namespace EAFCMatchTracker.Application.Interfaces.Repositories;

public interface IAppSettingRepository
{
    Task<List<AppSettingEntity>> GetAllAsync(CancellationToken ct);
    Task<AppSettingEntity?> GetByKeyAsync(string key, CancellationToken ct);
    Task UpsertAsync(string key, string value, CancellationToken ct);
}
