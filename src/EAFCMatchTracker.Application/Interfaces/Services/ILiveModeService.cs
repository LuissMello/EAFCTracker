namespace EAFCMatchTracker.Application.Interfaces.Services;

public sealed record LiveStatus(bool Enabled, DateTimeOffset? UntilUtc, int IntervalMinutes);

public sealed record FetchSchedule(
    LiveStatus Live,
    int FetchIntervalMinutes,
    int MaxParallelFetches)
{
    /// <summary>Intervalo efetivo entre ciclos (curto durante o modo ao vivo).</summary>
    public int EffectiveIntervalMinutes => Live.Enabled ? Live.IntervalMinutes : FetchIntervalMinutes;
}

/// <summary>
/// Modo "Jogos em andamento" global, persistido em AppSettings (live_enabled). Fica ligado até alguém desligar;
/// <see cref="LiveStatus.UntilUtc"/> é sempre null (mantido só por compatibilidade do contrato da API).
/// </summary>
public interface ILiveModeService
{
    Task<LiveStatus> GetStatusAsync(CancellationToken ct);
    Task<LiveStatus> SetAsync(bool enabled, CancellationToken ct);
    Task<FetchSchedule> GetScheduleAsync(CancellationToken ct);
}
