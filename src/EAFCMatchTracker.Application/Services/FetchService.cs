using EAFCMatchTracker.Application.Interfaces;
using EAFCMatchTracker.Application.Interfaces.Repositories;
using EAFCMatchTracker.Application.Interfaces.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EAFCMatchTracker.Application.Services;

public class FetchService : IFetchService
{
    private const string ClubsPath = "EAFCBackgroundWorkerSettings:ClubIds";

    private readonly IConfiguration _config;
    private readonly IClubMatchService _clubMatchService;
    private readonly IFetchRepository _fetchRepository;
    private readonly ILogger<FetchService> _logger;

    public FetchService(
        IConfiguration config,
        IClubMatchService clubMatchService,
        IFetchRepository fetchRepository,
        ILogger<FetchService> logger)
    {
        _config = config;
        _clubMatchService = clubMatchService;
        _fetchRepository = fetchRepository;
        _logger = logger;
    }

    public async Task<FetchRunResult> RunAsync(CancellationToken ct)
    {
        _logger.LogInformation("FetchService.RunAsync iniciado.");

        var clubIds = ParseClubIds();
        if (clubIds.Count == 0)
        {
            _logger.LogWarning("Nenhum ClubId configurado em {Path}.", ClubsPath);
            throw new InvalidOperationException("Nenhum ClubId configurado em " + ClubsPath + ".");
        }

        var types = new[] { "leagueMatch", "playoffMatch" };
        var errors = new List<string>();

        foreach (var t in types)
        {
            foreach (var cid in clubIds)
            {
                try
                {
                    _logger.LogInformation(
                        "Buscando e armazenando partidas (ClubId={ClubId}, Type={Type})",
                        cid, t);

                    await _clubMatchService.FetchAndStoreMatchesAsync(cid, t, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    _logger.LogWarning(
                        "Operação cancelada durante busca/armazenamento (ClubId={ClubId}, Type={Type})",
                        cid, t);
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Erro ao buscar/armazenar (ClubId={ClubId}, Type={Type})",
                        cid, t);

                    errors.Add($"{cid}:{t} - {ex.Message}");
                }
            }
        }

        var now = DateTimeOffset.UtcNow;

        try
        {
            await _fetchRepository.UpsertAuditAsync(now, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _logger.LogWarning("Operação cancelada durante atualização de auditoria de busca.");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar auditoria de busca.");
            errors.Add($"AuditUpdate - {ex.Message}");
        }

        _logger.LogInformation("FetchService.RunAsync finalizado. Erros: {ErrorCount}", errors.Count);

        return new FetchRunResult(now, errors.Count > 0, errors);
    }

    public async Task<DateTimeOffset?> GetLastRunAsync(CancellationToken ct)
    {
        _logger.LogInformation("FetchService.GetLastRunAsync consultado.");
        var row = await _fetchRepository.GetAuditReadOnlyAsync(ct);
        return row?.LastFetchedAt;
    }

    private List<string> ParseClubIds()
    {
        var rawIds = _config[ClubsPath] ?? string.Empty;
        return rawIds
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
