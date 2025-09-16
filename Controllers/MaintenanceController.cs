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

    /// <summary>
    /// Reprocessa clubs conhecidos (presentes em MatchClubs), atualizando OverallStats e PlayoffAchievements 1:N.
    /// </summary>
    [HttpPost("clubs/overall/refresh")]
    public async Task<IActionResult> RefreshClubsOverall(CancellationToken ct)
    {
        var clubIds = await _db.MatchClubs
            .AsNoTracking()
            .Select(mc => mc.ClubId)
            .Distinct()
            .ToListAsync(ct);

        int processed = 0;
        int overallUpdated = 0;
        int playoffsInserted = 0;
        int playoffsUpdated = 0;

        // headers uma vez só
        EnsureDefaultHeaders(_http);

        foreach (var clubId in clubIds)
        {
            processed++;

            // busca overall e playoffs em paralelo
            var overallTask = FetchOverallDtoAsync(clubId, ct);
            var playoffTask = FetchPlayoffAchievementsAsync(clubId, ct);

            await Task.WhenAll(overallTask, playoffTask);

            var overallDto = overallTask.Result;
            var playoffDtos = playoffTask.Result;

            // ----- Overall upsert (comportamento que você já tinha) -----
            if (overallDto is not null)
            {
                var existing = await _db.OverallStats.FirstOrDefaultAsync(o => o.ClubId == clubId, ct);
                if (existing is null)
                {
                    var entity = MapToEntity(clubId, overallDto);
                    await _db.OverallStats.AddAsync(entity, ct);
                    overallUpdated++;
                }
                else
                {
                    MapToEntity(clubId, overallDto, existing);
                    _db.OverallStats.Update(existing);
                    overallUpdated++;
                }
            }

            // ----- Playoff achievements upsert 1:N -----
            if (playoffDtos is not null && playoffDtos.Count > 0)
            {
                var (ins, upd) = await UpsertPlayoffAchievementsAsync(clubId, playoffDtos, ct);
                playoffsInserted += ins;
                playoffsUpdated += upd;
            }

            // salva alterações do lote deste clube
            if (_db.ChangeTracker.HasChanges())
            {
                await _db.SaveChangesAsync(ct);
            }
        }

        return Ok(new
        {
            processed,
            overallUpdated,
            playoffsInserted,
            playoffsUpdated
        });
    }

    // --------- FETCHERS ---------

    private async Task<OverallStats?> FetchOverallDtoAsync(long clubId, CancellationToken ct)
    {
        var baseUrl = _config["EAFCSettings:BaseUrl"];
        var endpointTpl = _config["EAFCSettings:OverallStatsEndpoint"]
                          ?? _config["OverallStatsEndpoint"]
                          ?? "/clubs/overallStats?platform=common-gen5&clubIds={0}";

        var uri = new Uri($"{baseUrl!.TrimEnd('/')}/{string.Format(endpointTpl, clubId).TrimStart('/')}");

        var resp = await _http.GetAsync(uri, ct);
        if (!resp.IsSuccessStatusCode) return null;

        var json = await resp.Content.ReadAsStringAsync(ct);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        try
        {
            var list = JsonSerializer.Deserialize<List<OverallStats>>(json, options);
            if (list is { Count: > 0 }) return list[0];
        }
        catch
        {
            var single = JsonSerializer.Deserialize<OverallStats>(json, options);
            if (single != null) return single;
        }

        return null;
    }

    private async Task<List<PlayoffAchievement>?> FetchPlayoffAchievementsAsync(long clubId, CancellationToken ct)
    {
        var baseUrl = _config["EAFCSettings:BaseUrl"];
        var endpointTpl = _config["EAFCSettings:PlayoffAchievementsEndpoint"]
                          ?? _config["PlayoffAchievementsEndpoint"]
                          ?? "/club/playoffAchievements?platform=common-gen5&clubId={0}";

        var uri = new Uri($"{baseUrl!.TrimEnd('/')}/{string.Format(endpointTpl, clubId).TrimStart('/')}");

        var resp = await _http.GetAsync(uri, ct);
        if (!resp.IsSuccessStatusCode) return null;

        var json = await resp.Content.ReadAsStringAsync(ct);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        // Pode ser lista ou item único
        try
        {
            var list = JsonSerializer.Deserialize<List<PlayoffAchievement>>(json, options);
            if (list is { Count: > 0 }) return list;
        }
        catch
        {
            var single = JsonSerializer.Deserialize<PlayoffAchievement>(json, options);
            if (single != null) return new List<PlayoffAchievement> { single };
        }

        return null;
    }

    // --------- UPSERTS ---------

    private async Task<(int inserted, int updated)> UpsertPlayoffAchievementsAsync(
        long clubId,
        IEnumerable<PlayoffAchievement> items,
        CancellationToken ct)
    {
        int inserted = 0, updated = 0;

        // carrega existentes do clube
        var existing = await _db.PlayoffAchievements
                                .Where(p => p.ClubId == clubId)
                                .ToListAsync(ct);

        var bySeason = existing.ToDictionary(p => p.SeasonId, StringComparer.OrdinalIgnoreCase);
        var utcNow = DateTime.UtcNow;

        foreach (var it in items)
        {
            if (string.IsNullOrWhiteSpace(it.SeasonId))
                continue; // precisa de SeasonId para unicidade

            if (bySeason.TryGetValue(it.SeasonId, out var row))
            {
                // update
                row.SeasonName = it.SeasonName;
                row.BestDivision = it.BestDivision;
                row.BestFinishGroup = it.BestFinishGroup;
                row.UpdatedAtUtc = utcNow;
                _db.PlayoffAchievements.Update(row);
                updated++;
            }
            else
            {
                // insert
                var entity = new PlayoffAchievementEntity
                {
                    ClubId = clubId,
                    SeasonId = it.SeasonId,
                    SeasonName = it.SeasonName,
                    BestDivision = it.BestDivision,
                    BestFinishGroup = it.BestFinishGroup,
                    RetrievedAtUtc = utcNow,
                    UpdatedAtUtc = utcNow
                };
                await _db.PlayoffAchievements.AddAsync(entity, ct);
                inserted++;
            }
        }

        return (inserted, updated);
    }

    // --------- MAPPING ---------

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

    // --------- UTILS ---------

    private static void EnsureDefaultHeaders(HttpClient http)
    {
        // evita acumular valores duplicados no User-Agent/Connection em loops
        if (!http.DefaultRequestHeaders.UserAgent.Any())
            http.DefaultRequestHeaders.UserAgent.ParseAdd("PostmanRuntime/7.36.0");

        if (!http.DefaultRequestHeaders.Connection.Any())
            http.DefaultRequestHeaders.Connection.ParseAdd("keep-alive");
    }
}
