
public class ClubMatchBackgroundService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly IConfiguration _config;

    public ClubMatchBackgroundService(IServiceProvider services, IConfiguration config)
    {
        _services = services;
        _config = config;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalMinutes = int.Parse(_config["EAFCBackgroundWorkerSettings:ExecutionIntervalInMinutes"]);
        var clubId = "3463149"; // pode vir de config também

        while (!stoppingToken.IsCancellationRequested)
        {
            using (var scope = _services.CreateScope())
            {
                var matchService = scope.ServiceProvider.GetRequiredService<ClubMatchService>();
                await matchService.FetchAndStoreMatchesAsync(clubId, "leagueMatch");
                await matchService.FetchAndStoreMatchesAsync(clubId, "playoffMatch");
            }

            await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken);
        }
    }
}