using System.Collections.Concurrent;
using System.Diagnostics;
using EAFCMatchTracker.Application.Interfaces;
using EAFCMatchTracker.Application.Interfaces.Repositories;
using EAFCMatchTracker.Application.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace EAFCMatchTracker.Application.Services;

/// <summary>
/// Única implementação de "rodar um ciclo de busca". Singleton com gate (1,1):
/// BackgroundService e FetchService nunca executam ciclos ao mesmo tempo (em processo).
/// </summary>
public sealed class FetchCoordinator : IFetchCoordinator
{
    private static readonly string[] MatchTypes = ["leagueMatch", "playoffMatch"];
    private static readonly TimeSpan ManualMinSpacing = TimeSpan.FromSeconds(60);
    private const string UniqueViolationSqlState = "23505";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILiveModeService _live;
    private readonly ILogger<FetchCoordinator> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private long _lastStartTicks; // 0 = nunca

    public FetchCoordinator(
        IServiceScopeFactory scopeFactory,
        ILiveModeService live,
        ILogger<FetchCoordinator> logger)
    {
        _scopeFactory = scopeFactory;
        _live = live;
        _logger = logger;
    }

    public bool IsRunning => _gate.CurrentCount == 0;

    public DateTimeOffset? LastCycleStartedUtc
    {
        get
        {
            var ticks = Interlocked.Read(ref _lastStartTicks);
            return ticks == 0 ? null : new DateTimeOffset(ticks, TimeSpan.Zero);
        }
    }

    public Task<FetchRunResult> RunManualAsync(CancellationToken ct)
    {
        var last = LastCycleStartedUtc;
        if (IsRunning || (last.HasValue && DateTimeOffset.UtcNow - last.Value < ManualMinSpacing))
        {
            _logger.LogInformation(
                "Busca manual ignorada: ciclo em andamento ou iniciado há menos de {Seconds}s.",
                ManualMinSpacing.TotalSeconds);
            return Task.FromResult(Skipped());
        }

        return RunCycleAsync(ct);
    }

    public Task<FetchRunResult> RunScheduledAsync(CancellationToken ct) => RunCycleAsync(ct);

    private static FetchRunResult Skipped() =>
        new(DateTimeOffset.UtcNow, false, Array.Empty<string>(), Skipped: true);

    private async Task<FetchRunResult> RunCycleAsync(CancellationToken ct)
    {
        if (!await _gate.WaitAsync(0, ct))
            return Skipped();

        try
        {
            return await ExecuteCycleAsync(ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<FetchRunResult> ExecuteCycleAsync(CancellationToken ct)
    {
        List<string> clubIds;

        using (var scope = _scopeFactory.CreateScope())
        {
            var clubRepo = scope.ServiceProvider.GetRequiredService<ITrackedClubRepository>();
            clubIds = (await clubRepo.GetAllAsync(ct)).Select(c => c.ClubId.ToString()).ToList();
        }

        if (clubIds.Count == 0)
        {
            _logger.LogWarning("Nenhum clube no tracking. Ciclo ignorado.");
            throw new NoTrackedClubsException();
        }

        var maxParallel = (await _live.GetScheduleAsync(ct)).MaxParallelFetches;

        Interlocked.Exchange(ref _lastStartTicks, DateTimeOffset.UtcNow.UtcTicks);

        var sw = Stopwatch.StartNew();
        var errors = new ConcurrentQueue<string>();
        var successes = 0;
        using var semaphore = new SemaphoreSlim(maxParallel, maxParallel);

        _logger.LogInformation(
            "Iniciando ciclo: {Clubs} clube(s) x {Types} tipo(s), paralelismo máx. {Max}",
            clubIds.Count, MatchTypes.Length, maxParallel);

        async Task RunOneAsync(string clubId, string matchType)
        {
            await semaphore.WaitAsync(ct);
            try
            {
                // Scope novo por tarefa: uma falha de save não contamina um DbContext compartilhado
                using var scope = _scopeFactory.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<IClubMatchService>();

                _logger.LogInformation("Fetching ClubId={ClubId} MatchType={MatchType}", clubId, matchType);
                await svc.FetchAndStoreMatchesAsync(clubId, matchType, ct);
                Interlocked.Increment(ref successes);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (IsUniqueViolation(ex))
            {
                _logger.LogDebug(
                    "Partida já armazenada (unique violation) ClubId={ClubId} MatchType={MatchType}",
                    clubId, matchType);
                Interlocked.Increment(ref successes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar ClubId={ClubId} MatchType={MatchType}", clubId, matchType);
                errors.Enqueue($"{clubId}:{matchType} - falha ao buscar/armazenar partidas");
            }
            finally
            {
                semaphore.Release();
            }
        }

        await Task.WhenAll(clubIds.SelectMany(c => MatchTypes.Select(t => RunOneAsync(c, t))));

        var now = DateTimeOffset.UtcNow;

        if (successes > 0)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var fetchRepo = scope.ServiceProvider.GetRequiredService<IFetchRepository>();
                await fetchRepo.UpsertAuditAsync(now, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao atualizar auditoria de busca.");
                errors.Enqueue("AuditUpdate - falha ao atualizar auditoria");
            }
        }
        else
        {
            _logger.LogWarning("Nenhuma tarefa de busca concluída com sucesso; auditoria não atualizada.");
        }

        await LinkGoalRegistrationsAsync(ct);

        _logger.LogInformation(
            "Ciclo concluído em {Elapsed}ms. Sucessos: {Ok}. Erros: {Errors}",
            sw.ElapsedMilliseconds, successes, errors.Count);

        var list = errors.ToList();
        return new FetchRunResult(now, list.Count > 0, list);
    }

    /// <summary>
    /// Vincula registros de gols pendentes às partidas recém-buscadas. Scope próprio; NUNCA faz o ciclo falhar.
    /// </summary>
    private async Task LinkGoalRegistrationsAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var linker = scope.ServiceProvider.GetRequiredService<IGoalRegistrationLinker>();
            await linker.RunAsync(ct: ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao vincular registros de gols ao fim do ciclo.");
        }
    }

    private static bool IsUniqueViolation(Exception ex)
    {
        for (Exception? e = ex; e is not null; e = e.InnerException)
        {
            if (e is PostgresException { SqlState: UniqueViolationSqlState })
                return true;
        }
        return false;
    }
}
