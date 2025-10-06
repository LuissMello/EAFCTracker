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
                    var matchType = parts.Length > 1 ? parts[1] : "leagueMatch";

                    await svc.FetchAndStoreMatchesAsync(clubId, matchType, stoppingToken);
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
