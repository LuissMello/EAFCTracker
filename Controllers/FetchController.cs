using EAFCMatchTracker.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/fetch")]
public class FetchController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly IClubMatchService _matchService;
    private readonly EAFCContext _db;
    private readonly ILogger<FetchController> _logger;

    private const string ClubsPath = "EAFCBackgroundWorkerSettings:ClubIds";

    public FetchController(
        IConfiguration config,
        IClubMatchService matchService,
        EAFCContext db,
        ILogger<FetchController> logger)
    {
        _config = config;
        _matchService = matchService;
        _db = db;
        _logger = logger;
    }

    // POST /api/fetch/run
    [HttpPost("run")]
    public async Task<IActionResult> Run(CancellationToken ct)
    {
        _logger.LogInformation("Iniciando execução de /api/fetch/run");

        List<string> clubIds;
        try
        {
            var rawIds = _config[ClubsPath] ?? string.Empty;
            clubIds = rawIds
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao ler ClubIds da configuração.");
            return StatusCode(500, "Erro ao ler ClubIds da configuração.");
        }

        if (clubIds.Count == 0)
        {
            _logger.LogWarning("Nenhum ClubId configurado em EAFCBackgroundWorkerSettings:ClubIds.");
            return BadRequest("Nenhum ClubId configurado em EAFCBackgroundWorkerSettings:ClubIds.");
        }
        var types = new[] { "leagueMatch", "playoffMatch" };
        var errors = new List<string>();

        foreach (var t in types) // primeiro TODOS leagueMatch, depois TODOS playoffMatch
        {
            foreach (var cid in clubIds)
            {
                try
                {
                    _logger.LogInformation(
                        "Buscando e armazenando partidas (ClubId={ClubId}, Type={Type})",
                        cid, t
                    );

                    await _matchService.FetchAndStoreMatchesAsync(cid, t, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    _logger.LogWarning(
                        "Operação cancelada durante busca/armazenamento (ClubId={ClubId}, Type={Type})",
                        cid, t
                    );
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Erro ao buscar/armazenar (ClubId={ClubId}, Type={Type})",
                        cid, t
                    );

                    errors.Add($"{cid}:{t} - {ex.Message}");
                }
            }
        }

        // Atualiza a "hora que buscou tudo" (mesmo se houveram erros, registramos a tentativa global):
        var now = DateTimeOffset.UtcNow;
        try
        {
            var row = await _db.SystemFetchAudits.SingleOrDefaultAsync(x => x.Id == 1, ct);
            if (row == null)
            {
                row = new SystemFetchAudit { Id = 1, LastFetchedAt = now };
                _db.SystemFetchAudits.Add(row);
                _logger.LogInformation("Criando novo registro de auditoria de busca.");
            }
            else
            {
                row.LastFetchedAt = now;
                _logger.LogInformation("Atualizando registro de auditoria de busca.");
            }
            await _db.SaveChangesAsync(ct);
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

        _logger.LogInformation("Execução de /api/fetch/run finalizada. Erros: {ErrorCount}", errors.Count);

        return Ok(new
        {
            ranAtUtc = now,
            hadErrors = errors.Count > 0,
            errors
        });
    }

    // GET /api/fetch/last-run
    [HttpGet("last-run")]
    public async Task<IActionResult> LastRun(CancellationToken ct)
    {
        _logger.LogInformation("Consultando última execução de busca (/api/fetch/last-run)");
        SystemFetchAudit? row = null;
        try
        {
            row = await _db.SystemFetchAudits.AsNoTracking()
                        .SingleOrDefaultAsync(x => x.Id == 1, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _logger.LogWarning("Operação cancelada durante consulta de auditoria de busca.");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao consultar auditoria de busca.");
            return StatusCode(500, "Erro ao consultar auditoria de busca.");
        }

        return Ok(new
        {
            lastFetchedAtUtc = row?.LastFetchedAt // null se nunca rodou
        });
    }
}
