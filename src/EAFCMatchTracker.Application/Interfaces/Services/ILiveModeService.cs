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

    /// <summary>
    /// Descarta o cache (TTL de 5 min) do agendamento e acorda quem espera em <see cref="WaitForChangeAsync"/>.
    /// Chamado de forma síncrona por TODA escrita em AppSettings neste processo (AppSettingRepository.UpsertAsync).
    /// </summary>
    void Invalidate();

    /// <summary>
    /// Espera até <paramref name="timeout"/> ou até alguém chamar <see cref="Invalidate"/> (configuração mudou). Devolve true se foi
    /// acordado por uma mudança. Usado pelo loop de busca para cortar o sono longo quando o modo ao vivo é ligado.
    /// </summary>
    Task<bool> WaitForChangeAsync(TimeSpan timeout, CancellationToken ct);
}
