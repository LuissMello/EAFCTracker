using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using EAFCMatchTracker.Infrastructure.Http;

namespace EAFCMatchTracker.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MaintenanceController : ControllerBase
{
    private readonly EAFCContext _db;
    private readonly IConfiguration _config;
    private readonly IEAHttpClient _eaHttpClient;

    public MaintenanceController(EAFCContext db, IConfiguration config, IEAHttpClient backend)
    {
        _db = db;
        _config = config;
        _eaHttpClient = backend;
    }

    [HttpPost("clubs/overall/refresh")]
    public async Task<IActionResult> RefreshClubsOverall(CancellationToken ct)
    {
        var clubIds = await _db.MatchClubs.AsNoTracking().Select(mc => mc.ClubId).Distinct().ToListAsync(ct);

        int processed = 0;
        int overallUpdated = 0;
        int playoffsInserted = 0;
        int playoffsUpdated = 0;

        foreach (var clubId in clubIds)
        {
            processed++;

            var overallTask = FetchOverallDtoAsync(clubId, ct);
            var playoffTask = FetchPlayoffAchievementsAsync(clubId, ct);

            await Task.WhenAll(overallTask, playoffTask);

            var overallDto = overallTask.Result;
            var playoffDtos = playoffTask.Result;

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

            if (playoffDtos is not null && playoffDtos.Count > 0)
            {
                var (ins, upd) = await UpsertPlayoffAchievementsAsync(clubId, playoffDtos, ct);
                playoffsInserted += ins;
                playoffsUpdated += upd;
            }

            if (_db.ChangeTracker.HasChanges())
                await _db.SaveChangesAsync(ct);
        }

        return Ok(new { processed, overallUpdated, playoffsInserted, playoffsUpdated });
    }

    [HttpPost("club/{clubId:long}/division/refresh")]
    public async Task<IActionResult> RefreshClubCurrentDivision(long clubId, [FromQuery] string? name, CancellationToken ct)
    {
        if (clubId <= 0) return BadRequest("Informe um clubId válido.");

        if (string.IsNullOrWhiteSpace(name))
        {
            name = await _db.MatchClubs
                            .AsNoTracking()
                            .Where(mc => mc.ClubId == clubId && mc.Details != null && mc.Details.Name != null)
                            .OrderByDescending(mc => mc.Id)
                            .Select(mc => mc.Details!.Name!)
                            .FirstOrDefaultAsync(ct);
        }

        if (string.IsNullOrWhiteSpace(name))
            return NotFound(new { message = "Nome do clube não encontrado em histórico e não foi informado via querystring." });

        var div = await FetchCurrentDivisionByNameAsync(name!, clubId, ct);
        if (!div.HasValue)
            return NotFound(new { message = "Divisão atual não encontrada na EA para este clube/nome." });

        var rows = await _db.OverallStats.Where(mc => mc.ClubId == clubId).ToListAsync(ct);
        if (rows.Count == 0)
            return NotFound(new { message = "Nenhum OverallStats encontrado para este clube." });

        foreach (var d in rows) d.CurrentDivision = div.Value;

        _db.OverallStats.UpdateRange(rows);
        await _db.SaveChangesAsync(ct);

        return Ok(new { clubId, clubName = name, currentDivision = div.Value, updatedRows = rows.Count });
    }

    [HttpPost("club/{clubId:long}/members/enrich")]
    public async Task<IActionResult> EnrichMatchPlayersWithMembers(long clubId, CancellationToken ct)
    {
        if (clubId <= 0) return BadRequest("Informe um clubId válido.");

        var members = await FetchMembersStatsAsync(clubId, ct);
        if (members is null || members.members.Count == 0)
            return NotFound(new { message = "Nenhum membro retornado pela EA para este clubId." });

        var byName = members.members
                            .Where(m => !string.IsNullOrWhiteSpace(m.name))
                            .GroupBy(m => m.name.Trim(), StringComparer.OrdinalIgnoreCase)
                            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var rows = await _db.MatchPlayers
                            .Include(mp => mp.Player)
                            .Where(mp => mp.ClubId == clubId && mp.Player != null)
                            .ToListAsync(ct);

        int updated = 0;
        foreach (var mp in rows)
        {
            var key = (mp.Player?.Playername ?? "").Trim();
            if (string.IsNullOrEmpty(key)) continue;

            if (byName.TryGetValue(key, out var mm))
            {
                var newOverall = ToNullableInt(mm.proOverall);
                var newHeight = ToNullableInt(mm.proHeight);
                var newStr = string.IsNullOrWhiteSpace(mm.proOverallStr) ? null : mm.proOverallStr;
                var newName = string.IsNullOrWhiteSpace(mm.proName) ? null : mm.proName;

                bool anyChange =
                    mp.ProOverall != newOverall ||
                    mp.ProHeight != newHeight ||
                    mp.ProOverallStr != newStr ||
                    mp.ProName != newName;

                if (anyChange)
                {
                    mp.ProOverall = newOverall;
                    mp.ProOverallStr = newStr;
                    mp.ProHeight = newHeight;
                    mp.ProName = newName;
                    updated++;
                }
            }
        }

        if (updated > 0)
            await _db.SaveChangesAsync(ct);

        return Ok(new { clubId, totalMatchPlayers = rows.Count, updated });
    }

    [HttpPost("club/{clubId:long}/refresh-external")]
    public async Task<IActionResult> RefreshClubExternal(long clubId, [FromQuery] string? name, CancellationToken ct)
    {
        if (clubId <= 0) return BadRequest("Informe um clubId válido.");

        var divResult = await RefreshClubCurrentDivision(clubId, name, ct) as OkObjectResult;
        var enrichResult = await EnrichMatchPlayersWithMembers(clubId, ct) as OkObjectResult;

        return Ok(new { division = divResult?.Value, members = enrichResult?.Value });
    }

    [HttpPost("club/{clubId:long}/opponents/division/refresh")]
    public async Task<IActionResult> RefreshOpponentsCurrentDivision(long clubId, CancellationToken ct)
    {
        if (clubId <= 0) return BadRequest("Informe um clubId válido.");

        var opponents = await
            (from mc in _db.MatchClubs.AsNoTracking()
             join my in _db.MatchClubs.AsNoTracking() on mc.MatchId equals my.MatchId
             where my.ClubId == clubId && mc.ClubId != clubId
             select new { OpponentId = mc.ClubId, Name = mc.Details != null ? mc.Details.Name : null, MatchClubId = mc.Id })
            .ToListAsync(ct);

        if (opponents.Count == 0)
            return Ok(new { clubId, opponentsFound = 0, updated = 0, detailsUpdated = 0 });

        var grouped = opponents
            .GroupBy(o => o.OpponentId)
            .Select(g =>
            {
                var name = g.Where(x => !string.IsNullOrWhiteSpace(x.Name))
                            .OrderByDescending(x => x.MatchClubId)
                            .Select(x => x.Name!)
                            .FirstOrDefault();
                return new { OpponentId = g.Key, Name = name };
            })
            .ToList();

        int updated = 0;
        int detailsUpdated = 0;
        var results = new List<object>();

        foreach (var opp in grouped)
        {
            var name = opp.Name ?? await _db.MatchClubs
                                            .AsNoTracking()
                                            .Where(x => x.ClubId == opp.OpponentId && x.Details != null && x.Details.Name != null)
                                            .OrderByDescending(x => x.Id)
                                            .Select(x => x.Details!.Name!)
                                            .FirstOrDefaultAsync(ct);

            if (string.IsNullOrWhiteSpace(name))
            {
                results.Add(new { opponentId = opp.OpponentId, status = "skipped_no_name" });
                continue;
            }

            var div = await FetchCurrentDivisionByNameAsync(name!, opp.OpponentId, ct);
            if (!div.HasValue)
            {
                results.Add(new { opponentId = opp.OpponentId, name, status = "not_found_in_ea" });
                continue;
            }

            var detailsRows = await _db.OverallStats.Where(mc => mc.ClubId == opp.OpponentId).ToListAsync(ct);
            if (detailsRows.Count == 0)
            {
                results.Add(new { opponentId = opp.OpponentId, name, currentDivision = div.Value, status = "no_overall_rows" });
                continue;
            }

            foreach (var d in detailsRows) d.CurrentDivision = div.Value;

            _db.OverallStats.UpdateRange(detailsRows);
            detailsUpdated += detailsRows.Count;
            updated++;

            results.Add(new { opponentId = opp.OpponentId, name, currentDivision = div.Value, detailsAffected = detailsRows.Count });
        }

        if (_db.ChangeTracker.HasChanges())
            await _db.SaveChangesAsync(ct);

        return Ok(new { clubId, opponentsFound = grouped.Count, updatedOpponents = updated, detailsUpdated, results });
    }

    [HttpPost("clubs/playoffs/refresh-all")]
    public async Task<IActionResult> RefreshAllPlayoffsAchievements(CancellationToken ct)
    {
        var clubIds = await _db.MatchClubs.AsNoTracking().Select(mc => mc.ClubId).Distinct().ToListAsync(ct);

        int processed = 0;
        int insertedTotal = 0;
        int updatedTotal = 0;

        foreach (var clubId in clubIds)
        {
            processed++;

            var playoffDtos = await FetchPlayoffAchievementsAsync(clubId, ct);
            if (playoffDtos is null || playoffDtos.Count == 0)
                continue;

            var (ins, upd) = await UpsertPlayoffAchievementsAsync(clubId, playoffDtos, ct);
            insertedTotal += ins;
            updatedTotal += upd;

            if (_db.ChangeTracker.HasChanges())
                await _db.SaveChangesAsync(ct);
        }

        return Ok(new { processedClubs = processed, playoffsInserted = insertedTotal, playoffsUpdated = updatedTotal });
    }

    [HttpPost("clubs/overall/refresh-all")]
    public async Task<IActionResult> RefreshAllOverallStats(CancellationToken ct)
    {
        var clubIds = await _db.MatchClubs.AsNoTracking().Select(mc => mc.ClubId).Distinct().ToListAsync(ct);

        int processed = 0;
        int overallInserted = 0;
        int overallUpdated = 0;

        foreach (var clubId in clubIds)
        {
            processed++;

            var overallDto = await FetchOverallDtoAsync(clubId, ct);
            if (overallDto is null) continue;

            var existing = await _db.OverallStats.FirstOrDefaultAsync(o => o.ClubId == clubId, ct);
            if (existing is null)
            {
                var entity = MapToEntity(clubId, overallDto);
                await _db.OverallStats.AddAsync(entity, ct);
                overallInserted++;
            }
            else
            {
                MapToEntity(clubId, overallDto, existing);
                _db.OverallStats.Update(existing);
                overallUpdated++;
            }

            if (_db.ChangeTracker.HasChanges())
                await _db.SaveChangesAsync(ct);
        }

        return Ok(new { processedClubs = processed, overallInserted, overallUpdated });
    }

    [HttpPost("clubs/division/refresh-all")]
    public async Task<IActionResult> RefreshAllCurrentDivisions(CancellationToken ct)
    {
        var clubIds = await _db.MatchClubs.AsNoTracking().Select(mc => mc.ClubId).Distinct().ToListAsync(ct);

        int processed = 0;
        int clubsWithDivision = 0;
        int detailsUpdated = 0;

        foreach (var clubId in clubIds)
        {
            processed++;

            var clubName = await _db.MatchClubs
                                    .AsNoTracking()
                                    .Where(mc => mc.ClubId == clubId && mc.Details != null && mc.Details.Name != null)
                                    .OrderByDescending(mc => mc.Id)
                                    .Select(mc => mc.Details!.Name!)
                                    .FirstOrDefaultAsync(ct);

            if (string.IsNullOrWhiteSpace(clubName)) continue;

            var div = await FetchCurrentDivisionByNameAsync(clubName, clubId, ct);
            if (!div.HasValue) continue;

            var rows = await _db.OverallStats.Where(mc => mc.ClubId == clubId).ToListAsync(ct);
            if (rows.Count == 0) continue;

            foreach (var d in rows) d.CurrentDivision = div.Value;

            _db.OverallStats.UpdateRange(rows);
            detailsUpdated += rows.Count;
            clubsWithDivision++;

            if (_db.ChangeTracker.HasChanges())
                await _db.SaveChangesAsync(ct);
        }

        return Ok(new { processedClubs = processed, clubsUpdated = clubsWithDivision, overallRowsUpdated = detailsUpdated });
    }

    [HttpPost("clubs/members/enrich-all")]
    public async Task<IActionResult> EnrichAllMatchPlayersWithMembers(CancellationToken ct)
    {
        var clubIds = await _db.MatchClubs.AsNoTracking().Select(mc => mc.ClubId).Distinct().ToListAsync(ct);

        int processed = 0;
        int clubsUpdated = 0;
        int totalPlayersUpdated = 0;

        foreach (var clubId in clubIds)
        {
            processed++;

            var members = await FetchMembersStatsAsync(clubId, ct);
            if (members is null || members.members.Count == 0) continue;

            var byName = members.members
                                .Where(m => !string.IsNullOrWhiteSpace(m.name))
                                .GroupBy(m => m.name.Trim(), StringComparer.OrdinalIgnoreCase)
                                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var rows = await _db.MatchPlayers
                                .Include(mp => mp.Player)
                                .Where(mp => mp.ClubId == clubId && mp.Player != null)
                                .ToListAsync(ct);

            int updated = 0;
            foreach (var mp in rows)
            {
                var key = (mp.Player?.Playername ?? "").Trim();
                if (string.IsNullOrEmpty(key)) continue;

                if (byName.TryGetValue(key, out var mm))
                {
                    var newOverall = ToNullableInt(mm.proOverall);
                    var newHeight = ToNullableInt(mm.proHeight);
                    var newStr = string.IsNullOrWhiteSpace(mm.proOverallStr) ? null : mm.proOverallStr;
                    var newName = string.IsNullOrWhiteSpace(mm.proName) ? null : mm.proName;

                    bool anyChange =
                        mp.ProOverall != newOverall ||
                        mp.ProHeight != newHeight ||
                        mp.ProOverallStr != newStr ||
                        mp.ProName != newName;

                    if (anyChange)
                    {
                        mp.ProOverall = newOverall;
                        mp.ProOverallStr = newStr;
                        mp.ProHeight = newHeight;
                        mp.ProName = newName;
                        updated++;
                    }
                }
            }

            if (updated > 0)
            {
                await _db.SaveChangesAsync(ct);
                totalPlayersUpdated += updated;
                clubsUpdated++;
            }
        }

        return Ok(new { processedClubs = processed, clubsUpdated, playersUpdated = totalPlayersUpdated });
    }

    [HttpPost("clubs/refresh-everything")]
    public async Task<IActionResult> RefreshEverything(CancellationToken ct)
    {
        var clubIds = await _db.MatchClubs.AsNoTracking().Select(mc => mc.ClubId).Distinct().ToListAsync(ct);

        int processed = 0;
        int overallInserted = 0;
        int overallUpdated = 0;
        int playoffsInserted = 0;
        int playoffsUpdated = 0;
        int clubsDivisionUpdated = 0;
        int overallRowsDivisionUpdated = 0;
        int clubsMembersUpdated = 0;
        int playersUpdated = 0;

        foreach (var clubId in clubIds)
        {
            processed++;

            var overallTask = FetchOverallDtoAsync(clubId, ct);
            var playoffsTask = FetchPlayoffAchievementsAsync(clubId, ct);
            var clubNameTask = _db.MatchClubs
                                  .AsNoTracking()
                                  .Where(mc => mc.ClubId == clubId && mc.Details != null && mc.Details.Name != null)
                                  .OrderByDescending(mc => mc.Id)
                                  .Select(mc => mc.Details!.Name!)
                                  .FirstOrDefaultAsync(ct);

            await Task.WhenAll(overallTask, playoffsTask, clubNameTask);

            var overallDto = overallTask.Result;
            if (overallDto is not null)
            {
                var existing = await _db.OverallStats.FirstOrDefaultAsync(o => o.ClubId == clubId, ct);
                if (existing is null)
                {
                    var entity = MapToEntity(clubId, overallDto);
                    await _db.OverallStats.AddAsync(entity, ct);
                    overallInserted++;
                }
                else
                {
                    MapToEntity(clubId, overallDto, existing);
                    _db.OverallStats.Update(existing);
                    overallUpdated++;
                }
            }

            var playoffDtos = playoffsTask.Result;
            if (playoffDtos is not null && playoffDtos.Count > 0)
            {
                var (ins, upd) = await UpsertPlayoffAchievementsAsync(clubId, playoffDtos, ct);
                playoffsInserted += ins;
                playoffsUpdated += upd;
            }

            var clubName = clubNameTask.Result;
            if (!string.IsNullOrWhiteSpace(clubName))
            {
                var div = await FetchCurrentDivisionByNameAsync(clubName!, clubId, ct);
                if (div.HasValue)
                {
                    var rows = await _db.OverallStats.Where(mc => mc.ClubId == clubId).ToListAsync(ct);
                    if (rows.Count > 0)
                    {
                        foreach (var d in rows) d.CurrentDivision = div.Value;
                        _db.OverallStats.UpdateRange(rows);
                        overallRowsDivisionUpdated += rows.Count;
                        clubsDivisionUpdated++;
                    }
                }
            }

            var members = await FetchMembersStatsAsync(clubId, ct);
            if (members is not null && members.members.Count > 0)
            {
                var byName = members.members
                                    .Where(m => !string.IsNullOrWhiteSpace(m.name))
                                    .GroupBy(m => m.name.Trim(), StringComparer.OrdinalIgnoreCase)
                                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                var rows = await _db.MatchPlayers
                                    .Include(mp => mp.Player)
                                    .Where(mp => mp.ClubId == clubId && mp.Player != null)
                                    .ToListAsync(ct);

                int updated = 0;
                foreach (var mp in rows)
                {
                    var key = (mp.Player?.Playername ?? "").Trim();
                    if (string.IsNullOrEmpty(key)) continue;

                    if (byName.TryGetValue(key, out var mm))
                    {
                        var newOverall = ToNullableInt(mm.proOverall);
                        var newHeight = ToNullableInt(mm.proHeight);
                        var newStr = string.IsNullOrWhiteSpace(mm.proOverallStr) ? null : mm.proOverallStr;
                        var newName = string.IsNullOrWhiteSpace(mm.proName) ? null : mm.proName;

                        bool anyChange =
                            mp.ProOverall != newOverall ||
                            mp.ProHeight != newHeight ||
                            mp.ProOverallStr != newStr ||
                            mp.ProName != newName;

                        if (anyChange)
                        {
                            mp.ProOverall = newOverall;
                            mp.ProOverallStr = newStr;
                            mp.ProHeight = newHeight;
                            mp.ProName = newName;
                            updated++;
                        }
                    }
                }

                if (updated > 0)
                {
                    playersUpdated += updated;
                    clubsMembersUpdated++;
                }
            }

            if (_db.ChangeTracker.HasChanges())
                await _db.SaveChangesAsync(ct);
        }

        return Ok(new
        {
            processedClubs = processed,
            overall = new { inserted = overallInserted, updated = overallUpdated },
            playoffs = new { inserted = playoffsInserted, updated = playoffsUpdated },
            divisions = new { clubsUpdated = clubsDivisionUpdated, overallRowsUpdated = overallRowsDivisionUpdated },
            members = new { clubsUpdated = clubsMembersUpdated, playersUpdated }
        });
    }

    private async Task<OverallStats?> FetchOverallDtoAsync(long clubId, CancellationToken ct)
    {
        var baseUrl = _config["EAFCSettings:BaseUrl"];
        var endpointTpl = _config["EAFCSettings:OverallStatsEndpoint"]
                          ?? _config["OverallStatsEndpoint"]
                          ?? "/clubs/overallStats?platform=common-gen5&clubIds={0}";

        EnsureBaseUrl(baseUrl);
        var uri = BuildUri(baseUrl!, string.Format(endpointTpl, clubId));

        var json = await _eaHttpClient.GetStringAsync(uri, ct);
        if (json is null) return null;

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        try
        {
            var list = System.Text.Json.JsonSerializer.Deserialize<List<OverallStats>>(json, options);
            if (list is { Count: > 0 }) return list[0];
        }
        catch
        {
            var single = System.Text.Json.JsonSerializer.Deserialize<OverallStats>(json, options);
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

        EnsureBaseUrl(baseUrl);
        var uri = BuildUri(baseUrl!, string.Format(endpointTpl, clubId));

        var json = await _eaHttpClient.GetStringAsync(uri, ct);
        if (json is null) return null;

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        try
        {
            var list = System.Text.Json.JsonSerializer.Deserialize<List<PlayoffAchievement>>(json, options);
            if (list is { Count: > 0 }) return list;
        }
        catch
        {
            var single = System.Text.Json.JsonSerializer.Deserialize<PlayoffAchievement>(json, options);
            if (single != null) return new List<PlayoffAchievement> { single };
        }

        return null;
    }

    private sealed class SearchClubResult
    {
        public string clubId { get; set; } = default!;
        public string? currentDivision { get; set; }
        public SearchClubInfo? clubInfo { get; set; }
        public string? clubName { get; set; }
    }

    private sealed class SearchClubInfo
    {
        public string? name { get; set; }
        public long clubId { get; set; }
    }

    private sealed class MembersStatsResponse
    {
        public List<MemberStats> members { get; set; } = new();
    }

    private sealed class MemberStats
    {
        public string name { get; set; } = default!;
        public string? proOverall { get; set; }
        public string? proOverallStr { get; set; }
        public string? proHeight { get; set; }
        public string? proName { get; set; }
    }

    private async Task<int?> FetchCurrentDivisionByNameAsync(string clubName, long clubId, CancellationToken ct)
    {
        var baseUrl = _config["EAFCSettings:BaseUrl"];
        var searchTpl = _config["EAFCSettings:SearchClubsEndpoint"]
                        ?? "/allTimeLeaderboard/search?platform=common-gen5&clubName={0}";

        EnsureBaseUrl(baseUrl);
        var uri = BuildUri(baseUrl!, string.Format(searchTpl, Uri.EscapeDataString(clubName)));

        var json = await _eaHttpClient.GetStringAsync(uri, ct);
        if (json is null) return null;

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        List<SearchClubResult> payload;

        try
        {
            payload = System.Text.Json.JsonSerializer.Deserialize<List<SearchClubResult>>(json, options) ?? new();
        }
        catch
        {
            payload = new();
        }

        var byId = payload.FirstOrDefault(x => long.TryParse(x.clubId, out var id) && id == clubId);
        var pick = byId ?? payload.FirstOrDefault();
        if (pick == null) return null;

        return ToNullableInt(pick.currentDivision);
    }

    private async Task<MembersStatsResponse?> FetchMembersStatsAsync(long clubId, CancellationToken ct)
    {
        var baseUrl = _config["EAFCSettings:BaseUrl"];
        var membersTpl = _config["EAFCSettings:MembersStatsEndpoint"]
                         ?? "/members/stats?platform=common-gen5&clubId={0}";

        EnsureBaseUrl(baseUrl);
        var uri = BuildUri(baseUrl!, string.Format(membersTpl, clubId));

        var json = await _eaHttpClient.GetStringAsync(uri, ct);
        if (json is null) return null;

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<MembersStatsResponse>(json, options);
        }
        catch
        {
            return null;
        }
    }

    private async Task<(int inserted, int updated)> UpsertPlayoffAchievementsAsync(long clubId, IEnumerable<PlayoffAchievement> items, CancellationToken ct)
    {
        int inserted = 0, updated = 0;

        var existing = await _db.PlayoffAchievements.Where(p => p.ClubId == clubId).ToListAsync(ct);

        var bySeason = existing.ToDictionary(p => p.SeasonId, StringComparer.OrdinalIgnoreCase);
        var utcNow = DateTime.UtcNow;

        foreach (var it in items)
        {
            if (string.IsNullOrWhiteSpace(it.SeasonId)) continue;

            if (bySeason.TryGetValue(it.SeasonId, out var row))
            {
                row.SeasonName = it.SeasonName;
                row.BestDivision = it.BestDivision;
                row.BestFinishGroup = it.BestFinishGroup;
                row.UpdatedAtUtc = utcNow;
                _db.PlayoffAchievements.Update(row);
                updated++;
            }
            else
            {
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

    private static void EnsureBaseUrl(string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("EAFCSettings:BaseUrl não configurado.");
    }

    private static Uri BuildUri(string baseUrl, string relativeOrAbsolute)
    {
        var baseUri = new Uri(baseUrl.TrimEnd('/') + "/", UriKind.Absolute);
        if (Uri.TryCreate(relativeOrAbsolute, UriKind.Absolute, out var abs))
            return abs;

        return new Uri(baseUri, relativeOrAbsolute.TrimStart('/'));
    }

    private static int? ToNullableInt(string? s) => int.TryParse(s, out var v) ? v : (int?)null;
}
