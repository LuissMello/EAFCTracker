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
        var rawIds = _config[ClubsPath] ?? string.Empty;
        var clubIds = rawIds
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (clubIds.Count == 0)
            return BadRequest("Nenhum ClubId configurado em EAFCBackgroundWorkerSettings:ClubIds.");

        var types = new[] { "leagueMatch", "playoffMatch" };
        var errors = new List<string>();

        foreach (var cid in clubIds)
        {
            foreach (var t in types)
            {
                try
                {
                    await _matchService.FetchAndStoreMatchesAsync(cid, t, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao buscar/armazenar (ClubId={ClubId}, Type={Type})", cid, t);
                    errors.Add($"{cid}:{t} - {ex.Message}");
                }
            }
        }

        // Atualiza a "hora que buscou tudo" (mesmo se houveram erros, registramos a tentativa global):
        var now = DateTimeOffset.UtcNow;
        var row = await _db.SystemFetchAudits.SingleOrDefaultAsync(x => x.Id == 1, ct);
        if (row == null)
        {
            row = new SystemFetchAudit { Id = 1, LastFetchedAt = now };
            _db.SystemFetchAudits.Add(row);
        }
        else
        {
            row.LastFetchedAt = now;
        }
        await _db.SaveChangesAsync(ct);

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
        var row = await _db.SystemFetchAudits.AsNoTracking()
                    .SingleOrDefaultAsync(x => x.Id == 1, ct);

        return Ok(new
        {
            lastFetchedAtUtc = row?.LastFetchedAt // null se nunca rodou
        });
    }
}
