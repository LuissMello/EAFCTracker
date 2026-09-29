using EAFCMatchTracker.Application.Interfaces.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EAFCMatchTracker.Application.Services;

/// <summary>
/// Loop curto (tick) que decide se um ciclo de busca está devido. Intervalo normal =
/// fetch_interval_minutes; durante o modo "ao vivo" = live_interval_minutes. Enquanto ao vivo,
/// também faz um GET periódico no próprio /health para impedir que a máquina Fly.io durma.
/// </summary>
public sealed class ClubMatchBackgroundService : BackgroundService
{
    public const string KeepAliveHttpClientName = "keepalive";

    private static readonly TimeSpan Tick = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan LiveStartMinAge = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan KeepAliveEvery = TimeSpan.FromSeconds(60);

    private readonly IFetchCoordinator _coordinator;
    private readonly ILiveModeService _live;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<ClubMatchBackgroundService> _logger;

    private bool _keepAliveWarned;

    public ClubMatchBackgroundService(
        IFetchCoordinator coordinator,
        ILiveModeService live,
        IHttpClientFactory httpFactory,
        IConfiguration config,
        ILogger<ClubMatchBackgroundService> logger)
    {
        _coordinator = coordinator;
        _live = live;
        _httpFactory = httpFactory;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var lastAttempt = DateTimeOffset.MinValue;
        var lastKeepAlive = DateTimeOffset.MinValue;
        var wasLive = false;

        // Executa imediatamente ao iniciar, sem aguardar o primeiro intervalo
        lastAttempt = DateTimeOffset.UtcNow;
        await RunCycleSafeAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(Tick, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                var schedule = await _live.GetScheduleAsync(stoppingToken);
                var now = DateTimeOffset.UtcNow;

                var lastCycle = _coordinator.LastCycleStartedUtc;
                var reference = lastCycle.HasValue && lastCycle.Value > lastAttempt ? lastCycle.Value : lastAttempt;
                var age = now - reference;

                var due = age >= TimeSpan.FromMinutes(schedule.EffectiveIntervalMinutes);

                // Modo ao vivo recém-ligado: roda prontamente se o último ciclo tem mais de 60s
                if (schedule.Live.Enabled && !wasLive && age >= LiveStartMinAge)
                    due = true;

                wasLive = schedule.Live.Enabled;

                if (schedule.Live.Enabled && now - lastKeepAlive >= KeepAliveEvery)
                {
                    lastKeepAlive = now;
                    await KeepAliveAsync(stoppingToken);
                }

                if (due)
                {
                    lastAttempt = DateTimeOffset.UtcNow;
                    await RunCycleSafeAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha no tick do agendador de busca.");
            }
        }
    }

    private async Task RunCycleSafeAsync(CancellationToken ct)
    {
        try
        {
            var result = await _coordinator.RunScheduledAsync(ct);
            if (result.Skipped)
                _logger.LogDebug("Ciclo agendado ignorado: já existe um ciclo em andamento.");
        }
        catch (NoTrackedClubsException)
        {
            // já logado pelo coordenador
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao executar ciclo de busca agendado.");
        }
    }

    private string? ResolveKeepAliveUrl()
    {
        var configured = _config["Live:KeepAliveUrl"];
        if (!string.IsNullOrWhiteSpace(configured))
            return configured.Trim();

        var app = Environment.GetEnvironmentVariable("FLY_APP_NAME");
        if (!string.IsNullOrWhiteSpace(app))
            return $"https://{app.Trim()}.fly.dev/health";

        return null;
    }

    private async Task KeepAliveAsync(CancellationToken ct)
    {
        var url = ResolveKeepAliveUrl();
        if (url is null)
        {
            if (!_keepAliveWarned)
            {
                _keepAliveWarned = true;
                _logger.LogWarning(
                    "Modo ao vivo ativo, mas nem Live:KeepAliveUrl nem FLY_APP_NAME estão definidos; keep-alive desativado.");
            }
            return;
        }

        try
        {
            var client = _httpFactory.CreateClient(KeepAliveHttpClientName);
            using var resp = await client.GetAsync(url, ct);
            _logger.LogDebug("Keep-alive {Status}", (int)resp.StatusCode);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Keep-alive falhou.");
        }
    }
}
