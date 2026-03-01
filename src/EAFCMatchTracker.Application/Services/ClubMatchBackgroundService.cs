using EAFCMatchTracker.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace EAFCMatchTracker.Application.Services;

public sealed class ClubMatchBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ClubMatchBackgroundService> _logger;
    private readonly TimeSpan _interval;
    private readonly string[] _clubIds;
    private readonly SemaphoreSlim _semaphore;

    private static readonly string[] MatchTypes = ["leagueMatch", "playoffMatch"];

    public ClubMatchBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<ClubMatchBackgroundService> logger,
        IConfiguration config)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        var intervalMinutes = config.GetValue<int?>("EAFCBackgroundWorkerSettings:ExecutionIntervalInMinutes") ?? 60;
        _interval = TimeSpan.FromMinutes(intervalMinutes);

        var clubsConfig = config["EAFCBackgroundWorkerSettings:ClubIds"] ?? "";
        _clubIds = clubsConfig
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(item => item.Split(':', StringSplitOptions.TrimEntries)[0])
            .Where(id => !string.IsNullOrEmpty(id))
            .ToArray();

        // Limita chamadas simultâneas à API da EA para não gerar rate limit
        // Configurável via EAFCBackgroundWorkerSettings:MaxParallelFetches (padrão: 4)
        var maxParallel = config.GetValue<int?>("EAFCBackgroundWorkerSettings:MaxParallelFetches") ?? 4;
        _semaphore = new SemaphoreSlim(maxParallel, maxParallel);

        if (_clubIds.Length == 0)
            _logger.LogWarning("Nenhum ClubId configurado em EAFCBackgroundWorkerSettings:ClubIds");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Executa imediatamente ao iniciar, sem aguardar o primeiro intervalo
        await RunFetchCycleAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }

            await RunFetchCycleAsync(stoppingToken);
        }
    }

    private async Task RunFetchCycleAsync(CancellationToken ct)
    {
        if (_clubIds.Length == 0) return;

        var totalTasks = _clubIds.Length * MatchTypes.Length;
        var sw = Stopwatch.StartNew();

        _logger.LogInformation(
            "Iniciando ciclo: {Clubs} clube(s) × {Types} tipo(s) = {Total} tarefa(s) em paralelo",
            _clubIds.Length, MatchTypes.Length, totalTasks);

        // Todas as combinações (clubId × matchType) rodam em paralelo,
        // limitadas pelo semáforo para não sobrecarregar a API da EA
        var tasks = _clubIds
            .SelectMany(clubId => MatchTypes.Select(matchType => (clubId, matchType)))
            .Select(pair => FetchWithSemaphoreAsync(pair.clubId, pair.matchType, ct));

        await Task.WhenAll(tasks);

        _logger.LogInformation("Ciclo concluído em {Elapsed}ms", sw.ElapsedMilliseconds);
    }

    private async Task FetchWithSemaphoreAsync(string clubId, string matchType, CancellationToken ct)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IClubMatchService>();

            _logger.LogInformation("Fetching ClubId={ClubId} MatchType={MatchType}", clubId, matchType);
            await svc.FetchAndStoreMatchesAsync(clubId, matchType, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar ClubId={ClubId} MatchType={MatchType}", clubId, matchType);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
