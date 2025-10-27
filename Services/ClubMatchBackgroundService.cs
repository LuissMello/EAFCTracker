// Services/Background/ClubMatchBackgroundService.cs
using EAFCMatchTracker.Services.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public sealed class ClubMatchBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ClubMatchBackgroundService> _logger;
    private readonly IConfiguration _config;

    public ClubMatchBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<ClubMatchBackgroundService> logger,
        IConfiguration config)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _config = config;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalMinutes = _config.GetValue<int?>("EAFCBackgroundWorkerSettings:ExecutionIntervalInMinutes") ?? 60;
        var clubsConfig = _config["EAFCBackgroundWorkerSettings:ClubIds"] ?? "";
        var clubIds = clubsConfig
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();

        if (clubIds.Length == 0)
            _logger.LogWarning("Nenhum ClubId configurado em EAFCBackgroundWorkerSettings:ClubIds");

        var defaultMatchTypes = new[] { "leagueMatch", "playoffMatch" };

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<IClubMatchService>();

                foreach (var item in clubIds)
                {
                    var parts = item.Split(':', StringSplitOptions.TrimEntries);
                    var clubId = parts[0];

                    foreach (var matchType in defaultMatchTypes)
                    {
                        try
                        {
                            _logger.LogInformation("Fetching matches for ClubId={ClubId} MatchType={MatchType}", clubId, matchType);
                            await svc.FetchAndStoreMatchesAsync(clubId, matchType, stoppingToken);
                        }
                        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Erro ao buscar partidas para ClubId={ClubId} tipo={MatchType}", clubId, matchType);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao executar ClubMatchBackgroundService");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken);
            }
            catch (TaskCanceledException) { }
        }
    }
}