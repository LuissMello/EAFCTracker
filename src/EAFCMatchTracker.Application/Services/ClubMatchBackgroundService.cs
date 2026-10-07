using EAFCMatchTracker.Application.Interfaces.Repositories;
using EAFCMatchTracker.Application.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EAFCMatchTracker.Application.Services;

/// <summary>
/// Loop curto (tick) que decide se um ciclo de busca está devido. Intervalo normal =
/// fetch_interval_minutes; durante o modo "ao vivo" = live_interval_minutes. Enquanto ao vivo,
/// também faz um GET periódico no próprio /health/live (sem banco) para impedir que a máquina Fly.io durma.
/// Economia de custo: o tick é de 20 s só com o modo ao vivo ligado (60 s caso contrário, e o agendamento vem do cache de 5 min
/// do <see cref="ILiveModeService"/>, então o banco não é lido a cada tick); mudar uma configuração acorda o loop na hora. No boot,
/// o primeiro ciclo só roda se o último ciclo persistido (SystemFetchAudit) for mais antigo que o intervalo efetivo.
/// </summary>
public class ClubMatchBackgroundService : BackgroundService
{
    public const string KeepAliveHttpClientName = "keepalive";

    /// <summary>Tick com o modo ao vivo ligado.</summary>
    internal static readonly TimeSpan LiveTick = TimeSpan.FromSeconds(20);
    /// <summary>Tick com o modo ao vivo desligado (o sono é cortado quando uma configuração muda).</summary>
    internal static readonly TimeSpan IdleTick = TimeSpan.FromSeconds(60);
    /// <summary>Tolerância a relógio: um último ciclo "no futuro" além disso é ignorado (roda o ciclo do boot).</summary>
    private static readonly TimeSpan FutureSkew = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan LiveStartMinAge = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan KeepAliveEvery = TimeSpan.FromSeconds(60);

    private readonly IFetchCoordinator _coordinator;
    private readonly IServiceScopeFactory? _scopeFactory;
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
        ILogger<ClubMatchBackgroundService> logger,
        IServiceScopeFactory? scopeFactory = null)
    {
        _scopeFactory = scopeFactory;
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
        var liveNow = false;

        // Ciclo do boot: só roda se o último ciclo persistido for mais antigo que o intervalo efetivo (ou não houver registro)
        try
        {
            var (skip, reference, schedule0) = await DecideBootCycleAsync(stoppingToken);
            liveNow = schedule0?.Live.Enabled ?? false;
            if (skip)
            {
                lastAttempt = reference;
                _logger.LogInformation(
                    "Ciclo do boot ignorado: último ciclo há {Age:0.#} min (intervalo efetivo {Interval} min).",
                    (DateTimeOffset.UtcNow - reference).TotalMinutes, schedule0!.EffectiveIntervalMinutes);
            }
            else
            {
                lastAttempt = DateTimeOffset.UtcNow;
                await RunCycleSafeAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // 20 s só com o modo ao vivo; 60 s sem ele. Mudança de configuração (Invalidate) corta a espera.
                await _live.WaitForChangeAsync(liveNow ? LiveTick : IdleTick, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                var schedule = await _live.GetScheduleAsync(stoppingToken);
                liveNow = schedule.Live.Enabled;
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

    /// <summary>
    /// Decide se o ciclo imediato do boot deve ser pulado. Fonte do "último ciclo": SystemFetchAudit (a mesma de
    /// GET /api/fetch/last-run), gravada ao fim de TODO ciclo com ao menos uma busca bem-sucedida, haja partidas novas ou não.
    /// Nunca pula sem registro persistido, com falha de leitura ou com relógio inconsistente.
    /// </summary>
    internal async Task<(bool Skip, DateTimeOffset Reference, FetchSchedule? Schedule)> DecideBootCycleAsync(CancellationToken ct)
    {
        FetchSchedule? schedule = null;
        try
        {
            schedule = await _live.GetScheduleAsync(ct);
            var last = await GetLastPersistedCycleAsync(ct);
            var now = DateTimeOffset.UtcNow;
            if (last.HasValue && last.Value <= now + FutureSkew
                && now - last.Value < TimeSpan.FromMinutes(schedule.EffectiveIntervalMinutes))
                return (true, last.Value, schedule);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Não foi possível ler o último ciclo persistido; executando o ciclo do boot.");
        }
        return (false, DateTimeOffset.MinValue, schedule);
    }

    /// <summary>Último ciclo concluído persistido (SystemFetchAudit); nulo se não houver. Sobrescrevível nos testes.</summary>
    protected internal virtual async Task<DateTimeOffset?> GetLastPersistedCycleAsync(CancellationToken ct)
    {
        if (_scopeFactory is null) return null;
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IFetchRepository>();
        return (await repo.GetAuditReadOnlyAsync(ct))?.LastFetchedAt;
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

    private string? ResolveKeepAliveUrl() =>
        ResolveKeepAliveUrl(_config, Environment.GetEnvironmentVariable("FLY_APP_NAME"));

    /// <summary>Live:KeepAliveUrl, ou https://{FLY_APP_NAME}.fly.dev/health/live (liveness sem banco, para não acordar o Neon).</summary>
    internal static string? ResolveKeepAliveUrl(IConfiguration config, string? flyAppName)
    {
        var configured = config["Live:KeepAliveUrl"];
        if (!string.IsNullOrWhiteSpace(configured))
            return configured.Trim();

        return string.IsNullOrWhiteSpace(flyAppName) ? null : $"https://{flyAppName.Trim()}.fly.dev/health/live";
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
