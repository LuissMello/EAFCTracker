using System.Globalization;
using EAFCMatchTracker.Application.Interfaces.Repositories;
using EAFCMatchTracker.Application.Interfaces.Services;
using EAFCMatchTracker.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace EAFCMatchTracker.Application.Services;

/// <summary>
/// Modo ao vivo + agendamento, lidos de AppSettings. O agendamento fica em cache de memória por <see cref="Ttl"/> (5 min) para
/// o loop de busca e os GET públicos de /api/fetch/live não baterem no banco (que, no Neon, impede o scale-to-zero). Toda
/// escrita de AppSettings neste processo chama <see cref="Invalidate"/> de forma síncrona (AppSettingRepository.UpsertAsync e
/// <see cref="SetAsync"/>), então quem alterou nunca vê valor velho; só alterações feitas por outro processo esperam o TTL.
/// </summary>
public sealed class LiveModeService : ILiveModeService
{
    public static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _time;
    private readonly object _lock = new();
    private readonly SemaphoreSlim _changed = new(0, int.MaxValue);
    private FetchSchedule? _cached;
    private DateTimeOffset _expiresAt;
    private long _version; // sobe a cada Invalidate: uma leitura em andamento não grava no cache se houve invalidação no meio

    public LiveModeService(IServiceScopeFactory scopeFactory, TimeProvider? time = null)
    {
        _scopeFactory = scopeFactory;
        _time = time ?? TimeProvider.System;
    }

    public async Task<LiveStatus> GetStatusAsync(CancellationToken ct) =>
        (await GetScheduleAsync(ct)).Live;

    public async Task<FetchSchedule> GetScheduleAsync(CancellationToken ct)
    {
        long versionAtStart;
        lock (_lock)
        {
            if (_cached is not null && _time.GetUtcNow() < _expiresAt) return _cached;
            versionAtStart = _version;
        }

        FetchSchedule schedule;
        using (var scope = _scopeFactory.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IAppSettingRepository>();
            schedule = Parse(await repo.GetAllAsync(ct), DateTimeOffset.UtcNow);
        }

        lock (_lock)
        {
            if (_version == versionAtStart)
            {
                _cached = schedule;
                _expiresAt = _time.GetUtcNow() + Ttl;
            }
        }
        return schedule;
    }

    public async Task<LiveStatus> SetAsync(bool enabled, CancellationToken ct)
    {
        using (var scope = _scopeFactory.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IAppSettingRepository>();
            await repo.UpsertAsync(AppSettingEntity.Keys.LiveEnabled, enabled ? "true" : "false", ct);
        }

        Invalidate(); // garante o valor novo mesmo que o repositório usado não notifique
        return (await GetScheduleAsync(ct)).Live;
    }

    public void Invalidate()
    {
        lock (_lock)
        {
            _version++;
            _cached = null;
        }
        // acorda o loop de busca (no máximo um sinal pendente)
        if (_changed.CurrentCount == 0) _changed.Release();
    }

    public Task<bool> WaitForChangeAsync(TimeSpan timeout, CancellationToken ct) => _changed.WaitAsync(timeout, ct);

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
