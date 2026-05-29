using EAFCMatchTracker.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace EAFCMatchTracker.Api.Controllers;

[ApiController]
[Route("api/fetch")]
public class FetchController : ControllerBase
{
    private readonly IFetchService _fetchService;
    private readonly ILogger<FetchController> _logger;

    public FetchController(
        IFetchService fetchService,
        ILogger<FetchController> logger)
    {
        _fetchService = fetchService;
        _logger = logger;
    }

    // POST /api/fetch/run
    [HttpPost("run")]
    public async Task<IActionResult> Run(CancellationToken ct)
    {
        _logger.LogInformation("Iniciando execução de /api/fetch/run");

        try
        {
            var result = await _fetchService.RunAsync(ct);
            return Ok(new
            {
                ranAtUtc = result.RanAtUtc,
                hadErrors = result.HadErrors,
                errors = result.Errors
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex.Message);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado em /api/fetch/run.");
            return StatusCode(500, "Erro interno ao executar busca de partidas.");
        }
    }

    // GET /api/fetch/last-run
    [HttpGet("last-run")]
    public async Task<IActionResult> LastRun(CancellationToken ct)
    {
        _logger.LogInformation("Consultando última execução de busca (/api/fetch/last-run)");
        try
        {
            var lastFetchedAt = await _fetchService.GetLastRunAsync(ct);
            return Ok(new { lastFetchedAtUtc = lastFetchedAt });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao consultar auditoria de busca.");
            return StatusCode(500, "Erro ao consultar auditoria de busca.");
        }
    }
}
