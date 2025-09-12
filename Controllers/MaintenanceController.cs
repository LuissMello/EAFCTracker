using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EAFCMatchTracker.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MaintenanceController : ControllerBase
{
    private readonly EAFCContext _db;
    private readonly IConfiguration _config;
    private readonly HttpClient _http;

    public MaintenanceController(EAFCContext db, IConfiguration config, HttpClient http)
    {
        _db = db;
        _config = config;
        _http = http;
    }

    [HttpPost("clubs/overall/refresh")]
    public async Task<IActionResult> RefreshClubsOverall(CancellationToken ct)
    {
        var clubIds = await _db.MatchClubs
            .AsNoTracking()
            .Select(mc => mc.ClubId)
            .Distinct()
            .ToListAsync(ct);

        int processed = 0;
        int updated = 0;

        foreach (var clubId in clubIds)
        {
            processed++;

            var dto = await FetchOverallDtoAsync(clubId, ct);
            if (dto is null) continue;

            var existing = await _db.OverallStats.FirstOrDefaultAsync(o => o.ClubId == clubId, ct);
            if (existing is null)
            {
                var entity = MapToEntity(clubId, dto);
                await _db.OverallStats.AddAsync(entity, ct);
                updated++;
            }
            else
            {
                MapToEntity(clubId, dto, existing);
                _db.OverallStats.Update(existing);
                updated++;
            }

            await _db.SaveChangesAsync(ct);
        }

        return Ok(new { processed, updated });
    }

    private async Task<OverallStats?> FetchOverallDtoAsync(long clubId, CancellationToken ct)
    {
        var baseUrl = _config["EAFCSettings:BaseUrl"];
        var endpointTpl = _config["EAFCSettings:OverallStatsEndpoint"]
                          ?? _config["OverallStatsEndpoint"]
                          ?? "/clubs/overallStats?platform=common-gen5&clubIds={0}";

        var uri = new Uri($"{baseUrl.TrimEnd('/')}/{string.Format(endpointTpl, clubId).TrimStart('/')}");


        _http.DefaultRequestHeaders.UserAgent.ParseAdd("PostmanRuntime/7.36.0");
        _http.DefaultRequestHeaders.Connection.ParseAdd("keep-alive");

        using var resp = await _http.GetAsync(uri, ct);
        if (!resp.IsSuccessStatusCode) return null;

        var json = await resp.Content.ReadAsStringAsync(ct);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        try
        {
            var list = JsonSerializer.Deserialize<List<OverallStats>>(json, options);
            if (list != null && list.Count > 0) return list[0];
        }
        catch
        {
            var single = JsonSerializer.Deserialize<OverallStats>(json, options);
            if (single != null) return single;
        }

        return null;
    }

    private static OverallStatsEntity MapToEntity(long clubId, OverallStats src, OverallStatsEntity? target = null)
    {
        var o = target ?? new OverallStatsEntity { ClubId = clubId };
        o.BestDivision = src.BestDivision;
        o.BestFinishGroup = src.BestFinishGroup;
        o.GamesPlayed = src.GamesPlayed;
        o.GamesPlayedPlayoff = src.GamesPlayedPlayoff;
        o.Goals = src.Goals;
        o.GoalsAgainst = src.GoalsAgainst;
        o.Promotions = src.Promotions;
        o.Relegations = src.Relegations;
        o.Losses = src.Losses;
        o.Ties = src.Ties;
        o.Wins = src.Wins;
        o.Wstreak = src.Wstreak;
        o.Unbeatenstreak = src.Unbeatenstreak;
        o.SkillRating = src.SkillRating;
        o.Reputationtier = src.Reputationtier;
        o.LeagueAppearances = src.LeagueAppearances;
        o.UpdatedAtUtc = DateTime.UtcNow;
        return o;
    }
}
