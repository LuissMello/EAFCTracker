using System.Globalization;
using EAFCMatchTracker.Application.Interfaces.Repositories;
using EAFCMatchTracker.Application.Interfaces.Services;
using EAFCMatchTracker.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace EAFCMatchTracker.Application.Services;

public sealed class LiveModeService : ILiveModeService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public LiveModeService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<LiveStatus> GetStatusAsync(CancellationToken ct) =>
        (await GetScheduleAsync(ct)).Live;

    public async Task<FetchSchedule> GetScheduleAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IAppSettingRepository>();
        var all = await repo.GetAllAsync(ct);
        return Parse(all, DateTimeOffset.UtcNow);
    }

    public async Task<LiveStatus> SetAsync(bool enabled, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IAppSettingRepository>();

        await repo.UpsertAsync(AppSettingEntity.Keys.LiveEnabled, enabled ? "true" : "false", ct);

        return Parse(await repo.GetAllAsync(ct), DateTimeOffset.UtcNow).Live;
    }

    private static FetchSchedule Parse(IEnumerable<AppSettingEntity> settings, DateTimeOffset now)
    {
        var map = settings.ToDictionary(s => s.Key, s => s.Value, StringComparer.Ordinal);

        int GetInt(string key)
        {
            var def = AppSettingEntity.Definitions.Find(key)!;
            return map.TryGetValue(key, out var raw)
                   && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
                   && v >= def.Min && v <= def.Max
                ? v
                : def.Default;
        }

        var fetchInterval = GetInt(AppSettingEntity.Keys.FetchIntervalMinutes);
        var maxParallel = GetInt(AppSettingEntity.Keys.MaxParallelFetches);
        var liveInterval = GetInt(AppSettingEntity.Keys.LiveIntervalMinutes);

        var enabled = map.TryGetValue(AppSettingEntity.Keys.LiveEnabled, out var raw)
                      && string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);

        var live = new LiveStatus(enabled, null, liveInterval);
        return new FetchSchedule(live, fetchInterval, maxParallel);
    }
}
