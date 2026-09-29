namespace EAFCMatchTracker.Application.Interfaces.Services;

public interface IFetchService
{
    /// <summary>
    /// Executa (ou pula, se já houver ciclo em andamento / muito recente) a busca e armazenamento
    /// de partidas para todos os clubes configurados.
    /// Retorna um objeto com a hora de execução e erros eventuais.
    /// </summary>
    Task<FetchRunResult> RunAsync(CancellationToken ct);

    /// <summary>
    /// Retorna a data/hora da última execução bem-sucedida (ou null se nunca executou).
    /// </summary>
    Task<DateTimeOffset?> GetLastRunAsync(CancellationToken ct);
}

public record FetchRunResult(
    DateTimeOffset RanAtUtc,
    bool HadErrors,
    IReadOnlyList<string> Errors,
    bool Skipped = false);
