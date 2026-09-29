using EAFCMatchTracker.Application.Interfaces.Repositories;
using EAFCMatchTracker.Application.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace EAFCMatchTracker.Application.Services;

public class FetchService : IFetchService
{
    private readonly IFetchCoordinator _coordinator;
    private readonly IFetchRepository _fetchRepository;
    private readonly ILogger<FetchService> _logger;

    public FetchService(
        IFetchCoordinator coordinator,
        IFetchRepository fetchRepository,
        ILogger<FetchService> logger)
    {
        _coordinator = coordinator;
        _fetchRepository = fetchRepository;
        _logger = logger;
    }

    public async Task<FetchRunResult> RunAsync(CancellationToken ct)
    {
        _logger.LogInformation("FetchService.RunAsync iniciado.");
        var result = await _coordinator.RunManualAsync(ct);
        _logger.LogInformation(
            "FetchService.RunAsync finalizado. Skipped={Skipped} Erros={ErrorCount}",
            result.Skipped, result.Errors.Count);
        return result;
    }

    public async Task<DateTimeOffset?> GetLastRunAsync(CancellationToken ct)
    {
        _logger.LogInformation("FetchService.GetLastRunAsync consultado.");
        var row = await _fetchRepository.GetAuditReadOnlyAsync(ct);
        return row?.LastFetchedAt;
    }
}
