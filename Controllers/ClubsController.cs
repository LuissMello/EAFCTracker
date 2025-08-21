using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/[controller]")]
public class ClubsController : ControllerBase
{
    private readonly EAFCContext _db;
    private readonly IConfiguration _config;

    public ClubsController(EAFCContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ClubListItemDto>>> GetAll()
    {
        // 1) Lê a lista do appsettings
        var ids = ParseClubIdsFromConfig(_config);
        if (ids.Count == 0)
            return Ok(Array.Empty<ClubListItemDto>());

        // 2) Projeta SOMENTE escalares + filtra pelos IDs informados
        var flat = await _db.MatchClubs
            .AsNoTracking()
            .Where(mc => ids.Contains(mc.ClubId)) // traduz para IN (...)
            .Select(mc => new
            {
                ClubId = mc.ClubId,
                Name = mc.Details.Name,          // owned type: coluna
                Crest = mc.Details.CrestAssetId, // owned type: coluna
                Ts = mc.Match.Timestamp          // para pegar o mais recente
            })
            .OrderByDescending(x => x.Ts)
            .ToListAsync();

        // 3) Agrupa em memória, escolhe nome/crest mais recente não vazio
        var clubs = flat
            .GroupBy(x => x.ClubId)
            .Select(g =>
            {
                var name = g.Select(x => x.Name)
                            .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n))
                           ?? $"Clube {g.Key}";
                var crest = g.Select(x => x.Crest)
                             .FirstOrDefault(c => !string.IsNullOrWhiteSpace(c));
                return new ClubListItemDto
                {
                    ClubId = g.Key,
                    Name = name,
                    CrestAssetId = crest
                };
            })
            .OrderBy(x => x.Name)
            .ToList();

        return Ok(clubs);
    }

    private static List<long> ParseClubIdsFromConfig(IConfiguration config)
    {
        // Suporta tanto a nova chave "ClubIds" (separado por ;) quanto a antiga "ClubId"
        var raw = config["EAFCBackgroundWorkerSettings:ClubIds"]
               ?? config["EAFCBackgroundWorkerSettings:ClubId"];

        if (string.IsNullOrWhiteSpace(raw))
            return new List<long>();

        // Aceita separadores ; , espaço, quebra de linha
        var parts = raw.Split(new[] { ';', ',', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        return parts
            .Select(p => long.TryParse(p.Trim(), out var v) ? (long?)v : null)
            .Where(v => v.HasValue && v.Value > 0)
            .Select(v => v!.Value)
            .Distinct()
            .ToList();
    }
}
