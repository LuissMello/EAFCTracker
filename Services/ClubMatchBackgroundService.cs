using EAFCMatchTracker.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class ClubMatchBackgroundService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly IConfiguration _config;
    private readonly ILogger<ClubMatchBackgroundService> _logger;

    private const string IntervalPath = "EAFCBackgroundWorkerSettings:ExecutionIntervalInMinutes";
    private const string ClubsPath = "EAFCBackgroundWorkerSettings:ClubIds";

    public ClubMatchBackgroundService(
        IServiceProvider services,
        IConfiguration config,
        ILogger<ClubMatchBackgroundService> logger)
    {
        _services = services;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ClubMatchBackgroundService iniciado.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var intervalMinutes = int.TryParse(_config[IntervalPath], out var m) ? m : 5;

                var rawIds = _config[ClubsPath] ?? string.Empty;
                var clubIds = rawIds
                    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (clubIds.Count == 0)
                {
                    _logger.LogWarning("Nenhum ClubId configurado em {Path}.", ClubsPath);
                }
                else
                {
                    using var scope = _services.CreateScope();
                    var matchService = scope.ServiceProvider.GetRequiredService<ClubMatchService>();

                    foreach (var clubId in clubIds)
                    {
                        try
                        {
                            _logger.LogInformation("Buscando partidas para ClubId={ClubId}...", clubId);
                            await matchService.FetchAndStoreMatchesAsync(clubId, "leagueMatch", stoppingToken);
                            await matchService.FetchAndStoreMatchesAsync(clubId, "playoffMatch", stoppingToken);
                            _logger.LogInformation("Concluído para ClubId={ClubId}.", clubId);
                        }
                        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                        {
                            // Encerrando serviço
                            throw;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Erro ao buscar/armazenar partidas para ClubId={ClubId}.", clubId);
                            // Continua com os demais clubes
                        }
                    }

                    try
                    {
                        var db = scope.ServiceProvider.GetRequiredService<EAFCContext>();
                        var now = DateTimeOffset.UtcNow;

                        var audit = await db.SystemFetchAudits
                            .SingleOrDefaultAsync(x => x.Id == 1, stoppingToken);

                        if (audit == null)
                        {
                            db.SystemFetchAudits.Add(new SystemFetchAudit
                            {
                                Id = 1,
                                LastFetchedAt = now
                            });
                        }
                        else
                        {
                            audit.LastFetchedAt = now;
                        }

                        await db.SaveChangesAsync(stoppingToken);
                        _logger.LogInformation("Última busca registrada em {WhenUtc} (UTC).", now);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Falha ao registrar a hora da última busca.");
                    }
                }

                _logger.LogInformation("Aguardando {Minutes} minuto(s) para próxima execução…", intervalMinutes);
                await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // encerrando graciosamente
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado no loop do background worker.");
                // Em caso de erro geral, aguarda um backoff curto para não “girar” sem parar
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }

        _logger.LogInformation("ClubMatchBackgroundService finalizado.");
    }
}
