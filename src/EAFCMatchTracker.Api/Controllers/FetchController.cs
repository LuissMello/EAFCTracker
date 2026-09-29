using EAFCMatchTracker.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace EAFCMatchTracker.Api.Controllers;

[ApiController]
[Route("api/fetch")]
public class FetchController : ControllerBase
{
    private readonly IFetchService _fetchService;
    private readonly ILiveModeService _liveMode;
    private readonly ILogger<FetchController> _logger;

    public FetchController(
        IFetchService fetchService,
        ILiveModeService liveMode,
        ILogger<FetchController> logger)
    {
        _fetchService = fetchService;
        _liveMode = liveMode;
        _logger = logger;
    }

    // POST /api/fetch/run  (público; limitado no servidor: 1 ciclo por vez / >= 60s entre inícios)
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
                errors = result.Errors,
                skipped = result.Skipped
            });
        }
        catch (NoTrackedClubsException ex)
        {
            _logger.LogWarning(ex, "Nenhum clube cadastrado para /api/fetch/run.");
            return BadRequest("Nenhum clube cadastrado na tabela de tracking.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
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
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Erro ao consultar auditoria de busca.");
            return StatusCode(500, "Erro ao consultar auditoria de busca.");
        }
    }

    // GET /api/fetch/live
    [HttpGet("live")]
    public async Task<IActionResult> GetLive(CancellationToken ct)
    {
        var status = await _liveMode.GetStatusAsync(ct);
        return Ok(ToResponse(status));
    }

    // POST /api/fetch/live  { "enabled": true|false }  (público)
    [HttpPost("live")]
    public async Task<IActionResult> SetLive([FromBody] SetLiveRequest body, CancellationToken ct)
    {
        var status = await _liveMode.SetAsync(body.Enabled, ct);
        _logger.LogInformation("Modo ao vivo {State} (até {Until:O})",
            status.Enabled ? "ativado" : "desativado", status.UntilUtc);
        return Ok(ToResponse(status));
    }

    private static object ToResponse(LiveStatus s) => new
    {
        enabled = s.Enabled,
        untilUtc = s.UntilUtc,
        intervalMinutes = s.IntervalMinutes
    };
}

public record SetLiveRequest(bool Enabled);
