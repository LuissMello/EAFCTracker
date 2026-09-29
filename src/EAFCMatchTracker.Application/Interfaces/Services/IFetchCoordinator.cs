namespace EAFCMatchTracker.Application.Interfaces.Services;

/// <summary>Lançada quando não há clubes cadastrados para executar um ciclo.</summary>
public sealed class NoTrackedClubsException : InvalidOperationException
{
    public NoTrackedClubsException() : base("Nenhum clube cadastrado na tabela de tracking.") { }
}

/// <summary>
/// Orquestra os ciclos de busca (single-flight, em processo): garante que o BackgroundService
/// e o endpoint manual nunca executem ciclos concorrentemente. Singleton.
/// </summary>
public interface IFetchCoordinator
{
    bool IsRunning { get; }

    /// <summary>Momento em que o último ciclo INICIOU (null se nenhum desde o boot).</summary>
    DateTimeOffset? LastCycleStartedUtc { get; }

    /// <summary>
    /// Ciclo manual: pula (Skipped=true) se já houver ciclo em andamento ou se o último
    /// iniciou há menos de 60s.
    /// </summary>
    Task<FetchRunResult> RunManualAsync(CancellationToken ct);

    /// <summary>Ciclo agendado: pula (Skipped=true) apenas se já houver ciclo em andamento.</summary>
    Task<FetchRunResult> RunScheduledAsync(CancellationToken ct);
}
