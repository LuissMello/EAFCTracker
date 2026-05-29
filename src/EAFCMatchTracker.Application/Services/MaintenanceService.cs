using EAFCMatchTracker.Application.Dtos;
using EAFCMatchTracker.Application.Interfaces.Repositories;
using EAFCMatchTracker.Application.Interfaces.Services;
using EAFCMatchTracker.Domain.Entities;
using EAFCMatchTracker.Domain.Models;
using EAFCMatchTracker.Infrastructure.Data;
using EAFCMatchTracker.Infrastructure.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace EAFCMatchTracker.Application.Services;

public class MaintenanceService : IMaintenanceService
{
    private readonly EAFCContext _db;
    private readonly IConfiguration _config;
    private readonly IEAHttpClient _eaHttpClient;
    private readonly IClubRepository _clubRepository;
    private readonly ILogger<MaintenanceService> _logger;

    public MaintenanceService(
        EAFCContext db,
        IConfiguration config,
        IEAHttpClient eaHttpClient,
        IClubRepository clubRepository,
        ILogger<MaintenanceService> logger)
    {
        _db = db;
        _config = config;
        _eaHttpClient = eaHttpClient;
        _clubRepository = clubRepository;
        _logger = logger;
    }

    // ─── Sealed private classes for deserialization ───────────────────────────

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

    // ─── Public service methods ───────────────────────────────────────────────

    public async Task<object> RefreshClubsOverallAsync(CancellationToken ct)
    {
        var clubIds = await _clubRepository.GetAllDistinctClubIdsAsync(ct);

        int processed = 0, overallUpdated = 0, playoffsInserted = 0, playoffsUpdated = 0;

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
                await UpsertOverallAsync(clubId, overallDto, ct);
                overallUpdated++;
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

        return new { processed, overallUpdated, playoffsInserted, playoffsUpdated };
    }

    public async Task<object> RefreshClubCurrentDivisionAsync(long clubId, string? name, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name))
            name = await _clubRepository.GetLatestClubNameAsync(clubId, ct);

        if (string.IsNullOrWhiteSpace(name))
            throw new KeyNotFoundException("Nome do clube não encontrado em histórico e não foi informado via querystring.");

        var div = await FetchCurrentDivisionByNameAsync(name!, clubId, ct);
        if (!div.HasValue)
            throw new KeyNotFoundException("Divisão atual não encontrada na EA para este clube/nome.");

        var rows = await _clubRepository.GetAllOverallStatsByClubIdAsync(clubId, ct);
        if (rows.Count == 0)
            throw new KeyNotFoundException("Nenhum OverallStats encontrado para este clube.");

        foreach (var d in rows) d.CurrentDivision = div.Value;
        await _clubRepository.UpdateOverallStatsRangeAsync(rows);
        await _clubRepository.SaveChangesAsync(ct);

        return new { clubId, clubName = name, currentDivision = div.Value, updatedRows = rows.Count };
    }

    public async Task<object> EnrichMatchPlayersWithMembersAsync(long clubId, CancellationToken ct)
    {
        var members = await FetchMembersStatsAsync(clubId, ct);
        if (members is null || members.members.Count == 0)
            throw new KeyNotFoundException("Nenhum membro retornado pela EA para este clubId.");

        var byName = members.members
            .Where(m => !string.IsNullOrWhiteSpace(m.name))
            .GroupBy(m => m.name.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var rows = await _db.MatchPlayers
            .Include(mp => mp.Player)
            .Where(mp => mp.ClubId == clubId && mp.Player != null)
            .ToListAsync(ct);

        int updated = EnrichPlayers(rows, byName);

        if (updated > 0)
            await _db.SaveChangesAsync(ct);

        return new { clubId, totalMatchPlayers = rows.Count, updated };
    }

    public async Task<object> RefreshOpponentsCurrentDivisionAsync(long clubId, CancellationToken ct)
    {
        var opponents = await
            (from mc in _db.MatchClubs.AsNoTracking()
             join my in _db.MatchClubs.AsNoTracking() on mc.MatchId equals my.MatchId
             where my.ClubId == clubId && mc.ClubId != clubId
             select new { OpponentId = mc.ClubId, Name = mc.Details != null ? mc.Details.Name : null, MatchClubId = mc.Id })
            .ToListAsync(ct);

        if (opponents.Count == 0)
            return new { clubId, opponentsFound = 0, updated = 0, detailsUpdated = 0 };

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

        int updated = 0, detailsUpdated = 0;
        var results = new List<object>();

        foreach (var opp in grouped)
        {
            var name = opp.Name ?? await _clubRepository.GetLatestClubNameAsync(opp.OpponentId, ct);

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

            var detailsRows = await _clubRepository.GetAllOverallStatsByClubIdAsync(opp.OpponentId, ct);
            if (detailsRows.Count == 0)
            {
                results.Add(new { opponentId = opp.OpponentId, name, currentDivision = div.Value, status = "no_overall_rows" });
                continue;
            }

            foreach (var d in detailsRows) d.CurrentDivision = div.Value;
            await _clubRepository.UpdateOverallStatsRangeAsync(detailsRows);
            detailsUpdated += detailsRows.Count;
            updated++;

            results.Add(new { opponentId = opp.OpponentId, name, currentDivision = div.Value, detailsAffected = detailsRows.Count });
        }

        if (_db.ChangeTracker.HasChanges())
            await _db.SaveChangesAsync(ct);

        return new { clubId, opponentsFound = grouped.Count, updatedOpponents = updated, detailsUpdated, results };
    }

    public async Task<object> RefreshAllPlayoffsAchievementsAsync(CancellationToken ct)
    {
        var clubIds = await _clubRepository.GetAllDistinctClubIdsAsync(ct);

        int processed = 0, insertedTotal = 0, updatedTotal = 0;

        foreach (var clubId in clubIds)
        {
            processed++;
            var playoffDtos = await FetchPlayoffAchievementsAsync(clubId, ct);
            if (playoffDtos is null || playoffDtos.Count == 0) continue;

            var (ins, upd) = await UpsertPlayoffAchievementsAsync(clubId, playoffDtos, ct);
            insertedTotal += ins;
            updatedTotal += upd;

            if (_db.ChangeTracker.HasChanges())
                await _db.SaveChangesAsync(ct);
        }

        return new { processedClubs = processed, playoffsInserted = insertedTotal, playoffsUpdated = updatedTotal };
    }

    public async Task<object> RefreshAllOverallStatsAsync(CancellationToken ct)
    {
        var clubIds = await _clubRepository.GetAllDistinctClubIdsAsync(ct);

        int processed = 0, overallInserted = 0, overallUpdated = 0;

        foreach (var clubId in clubIds)
        {
            processed++;
            var overallDto = await FetchOverallDtoAsync(clubId, ct);
            if (overallDto is null) continue;

            var existing = await _clubRepository.GetOverallStatsByClubIdAsync(clubId, ct);
            if (existing is null)
            {
                await _clubRepository.AddOverallStatsAsync(MapToEntity(clubId, overallDto), ct);
                overallInserted++;
            }
            else
            {
                MapToEntity(clubId, overallDto, existing);
                await _clubRepository.UpdateOverallStatsAsync(existing);
                overallUpdated++;
            }

            if (_db.ChangeTracker.HasChanges())
                await _db.SaveChangesAsync(ct);
        }

        return new { processedClubs = processed, overallInserted, overallUpdated };
    }

    public async Task<object> RefreshAllCurrentDivisionsAsync(CancellationToken ct)
    {
        var clubIds = await _clubRepository.GetAllDistinctClubIdsAsync(ct);

        int processed = 0, clubsWithDivision = 0, detailsUpdated = 0;

        foreach (var clubId in clubIds)
        {
            processed++;
            var clubName = await _clubRepository.GetLatestClubNameAsync(clubId, ct);
            if (string.IsNullOrWhiteSpace(clubName)) continue;

            var div = await FetchCurrentDivisionByNameAsync(clubName, clubId, ct);
            if (!div.HasValue) continue;

            var rows = await _clubRepository.GetAllOverallStatsByClubIdAsync(clubId, ct);
            if (rows.Count == 0) continue;

            foreach (var d in rows) d.CurrentDivision = div.Value;
            await _clubRepository.UpdateOverallStatsRangeAsync(rows);
            detailsUpdated += rows.Count;
            clubsWithDivision++;

            if (_db.ChangeTracker.HasChanges())
                await _db.SaveChangesAsync(ct);
        }

        return new { processedClubs = processed, clubsUpdated = clubsWithDivision, overallRowsUpdated = detailsUpdated };
    }

    public async Task<object> EnrichAllMatchPlayersWithMembersAsync(CancellationToken ct)
    {
        var clubIds = await _clubRepository.GetAllDistinctClubIdsAsync(ct);

        int processed = 0, clubsUpdated = 0, totalPlayersUpdated = 0;

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

            int updated = EnrichPlayers(rows, byName);

            if (updated > 0)
            {
                await _db.SaveChangesAsync(ct);
                totalPlayersUpdated += updated;
                clubsUpdated++;
            }
        }

        return new { processedClubs = processed, clubsUpdated, playersUpdated = totalPlayersUpdated };
    }

    public async Task<object> RefreshEverythingAsync(CancellationToken ct)
    {
        var clubIds = await _clubRepository.GetAllDistinctClubIdsAsync(ct);

        int processed = 0, overallInserted = 0, overallUpdated = 0;
        int playoffsInserted = 0, playoffsUpdated = 0;
        int clubsDivisionUpdated = 0, overallRowsDivisionUpdated = 0;
        int clubsMembersUpdated = 0, playersUpdated = 0;

        foreach (var clubId in clubIds)
        {
            processed++;

            var overallTask = FetchOverallDtoAsync(clubId, ct);
            var playoffsTask = FetchPlayoffAchievementsAsync(clubId, ct);
            var clubNameTask = _clubRepository.GetLatestClubNameAsync(clubId, ct);

            await Task.WhenAll(overallTask, playoffsTask, clubNameTask);

            var overallDto = overallTask.Result;
            if (overallDto is not null)
            {
                var existing = await _clubRepository.GetOverallStatsByClubIdAsync(clubId, ct);
                if (existing is null) { await _clubRepository.AddOverallStatsAsync(MapToEntity(clubId, overallDto), ct); overallInserted++; }
                else { MapToEntity(clubId, overallDto, existing); await _clubRepository.UpdateOverallStatsAsync(existing); overallUpdated++; }
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
                    var rows = await _clubRepository.GetAllOverallStatsByClubIdAsync(clubId, ct);
                    if (rows.Count > 0)
                    {
                        foreach (var d in rows) d.CurrentDivision = div.Value;
                        await _clubRepository.UpdateOverallStatsRangeAsync(rows);
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

                int updated = EnrichPlayers(rows, byName);
                if (updated > 0) { playersUpdated += updated; clubsMembersUpdated++; }
            }

            if (_db.ChangeTracker.HasChanges())
                await _db.SaveChangesAsync(ct);
        }

        return new
        {
            processedClubs = processed,
            overall = new { inserted = overallInserted, updated = overallUpdated },
            playoffs = new { inserted = playoffsInserted, updated = playoffsUpdated },
            divisions = new { clubsUpdated = clubsDivisionUpdated, overallRowsUpdated = overallRowsDivisionUpdated },
            members = new { clubsUpdated = clubsMembersUpdated, playersUpdated }
        };
    }

    public async Task<object> GetRecentMatchesWithAggregatesAsync(int count, CancellationToken ct)
    {
        _logger.LogInformation("MaintenanceService.GetRecentMatchesWithAggregatesAsync count={Count}", count);

        var matches = await _db.Matches
            .AsNoTracking()
            .OrderByDescending(m => m.Timestamp)
            .Take(count)
            .Include(m => m.Clubs).ThenInclude(c => c.Details)
            .Include(m => m.MatchPlayers).ThenInclude(mp => mp.Player)
            .ToListAsync(ct);

        var matchDtos = matches.Select(m =>
        {
            var clubs = m.Clubs.Select(c => new MatchClubDto
            {
                ClubId       = c.ClubId,
                Date         = c.Date,
                GameNumber   = c.GameNumber,
                Goals        = c.Goals,
                GoalsAgainst = c.GoalsAgainst,
                Losses       = c.Losses,
                MatchType    = c.MatchType,
                Result       = c.Result,
                Score        = c.Score,
                SeasonId     = c.SeasonId,
                Team         = c.Team,
                Ties         = c.Ties,
                Wins         = c.Wins,
                WinnerByDnf  = c.WinnerByDnf,
                Details      = c.Details == null ? null : new ClubDetailsDto
                {
                    ClubId            = c.Details.ClubId,
                    Name              = c.Details.Name,
                    RegionId          = c.Details.RegionId,
                    TeamId            = c.Details.TeamId,
                    StadName          = c.Details.StadName,
                    KitId             = c.Details.KitId,
                    CustomKitId       = c.Details.CustomKitId,
                    CustomAwayKitId   = c.Details.CustomAwayKitId,
                    CustomThirdKitId  = c.Details.CustomThirdKitId,
                    CustomKeeperKitId = c.Details.CustomKeeperKitId,
                    KitColor1         = c.Details.KitColor1,
                    KitColor2         = c.Details.KitColor2,
                    KitColor3         = c.Details.KitColor3,
                    KitColor4         = c.Details.KitColor4,
                    KitAColor1        = c.Details.KitAColor1,
                    KitAColor2        = c.Details.KitAColor2,
                    KitAColor3        = c.Details.KitAColor3,
                    KitAColor4        = c.Details.KitAColor4,
                    KitThrdColor1     = c.Details.KitThrdColor1,
                    KitThrdColor2     = c.Details.KitThrdColor2,
                    KitThrdColor3     = c.Details.KitThrdColor3,
                    KitThrdColor4     = c.Details.KitThrdColor4,
                    DCustomKit        = c.Details.DCustomKit,
                    CrestColor        = c.Details.CrestColor,
                    CrestAssetId      = c.Details.CrestAssetId,
                    SelectedKitType   = c.Details.SelectedKitType
                }
            }).ToList();

            var players = m.MatchPlayers.Select(p => new MatchPlayerDto
            {
                PlayerId       = p.Player?.PlayerId ?? p.PlayerEntityId,
                Id             = p.PlayerEntityId,
                ClubId         = p.ClubId,
                Playername     = p.Player?.Playername ?? p.ProName ?? p.PlayerEntityId.ToString(),
                Pos            = p.Pos,
                Namespace      = p.Namespace,
                Goals          = p.Goals,
                Assists        = p.Assists,
                PreAssists     = p.PreAssists,
                Rating         = p.Rating,
                Cleansheetsany = p.Cleansheetsany,
                Cleansheetsdef = p.Cleansheetsdef,
                Cleansheetsgk  = p.Cleansheetsgk,
                Losses         = p.Losses,
                Mom            = p.Mom,
                Passattempts   = p.Passattempts,
                Passesmade     = p.Passesmade,
                Realtimegame   = p.Realtimegame,
                Realtimeidle   = p.Realtimeidle,
                Redcards       = p.Redcards,
                Saves          = p.Saves,
                Score          = p.Score,
                Shots          = p.Shots,
                Tackleattempts = p.Tackleattempts,
                Tacklesmade    = p.Tacklesmade,
                Vproattr       = p.Vproattr,
                Vprohackreason = p.Vprohackreason,
                Wins           = p.Wins,
                Stats          = null
            }).ToList();

            var overall     = StatsAggregator.BuildOverallForSingleMatch(m.MatchPlayers);
            var playerStats = StatsAggregator.BuildPerPlayer(m.MatchPlayers, includeDisconnected: true);
            var clubStats   = StatsAggregator.BuildPerClub(m.MatchPlayers, m.Clubs.ToDictionary(c => c.ClubId));
            var eventAggregates = MatchAggregateParser.BuildClubAggregates(m.Clubs, m.MatchPlayers);

            return new FullMatchDataDto
            {
                MatchId         = m.MatchId,
                Timestamp       = m.Timestamp,
                MatchType       = m.MatchType.ToString(),
                Clubs           = clubs,
                Players         = players,
                Statistics      = new MatchStatisticsResponseDto
                {
                    Overall = overall,
                    Players = playerStats,
                    Clubs   = clubStats
                },
                EventAggregates = eventAggregates
            };
        }).ToList();

        return new RecentMatchesWithAggregatesDto
        {
            RequestedCount   = count,
            ReturnedCount    = matchDtos.Count,
            Categories       = MatchEventDefinitions.Categories.ToList(),
            EventDefinitions = MatchEventDefinitions.All.ToList(),
            Matches          = matchDtos
        };
    }

    // ─── Private helpers ──────────────────────────────────────────────────────

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

        EnsureBaseUrl(baseUrl);
        var uri = BuildUri(baseUrl!, string.Format(endpointTpl, clubId));
        var json = await _eaHttpClient.GetStringAsync(uri, ct);
        if (json is null) return null;

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
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

    private async Task<int?> FetchCurrentDivisionByNameAsync(string clubName, long clubId, CancellationToken ct)
    {
        var baseUrl = _config["EAFCSettings:BaseUrl"];
        var currentSeasonTpl = _config["EAFCSettings:SearchCurrentSeasonLeaderboardEndpoint"]
                               ?? "/currentSeasonLeaderboard/search?platform=common-gen5&clubName={0}";
        var allTimeTpl = _config["EAFCSettings:SearchAllTimeLeaderboardEndpoint"]
                         ?? _config["EAFCSettings:SearchClubsEndpoint"]
                         ?? "/allTimeLeaderboard/search?platform=common-gen5&clubName={0}";
        var threshold = int.TryParse(_config["EAFCSettings:DivisionThresholdForAllTime"], out var t) ? t : 5;

        EnsureBaseUrl(baseUrl);
        var uri = BuildUri(baseUrl!, string.Format(currentSeasonTpl, Uri.EscapeDataString(clubName)));
        var json = await _eaHttpClient.GetStringAsync(uri, ct);
        if (json is null) return null;

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        List<SearchClubResult> payload;
        try { payload = JsonSerializer.Deserialize<List<SearchClubResult>>(json, options) ?? new(); }
        catch { payload = new(); }

        var byId = payload.FirstOrDefault(x => long.TryParse(x.clubId, out var id) && id == clubId);
        var pick = byId ?? payload.FirstOrDefault();
        if (pick == null) return null;

        var division = ToNullableInt(pick.currentDivision);

        if (division.HasValue && division.Value > threshold)
        {
            var allTimeUri = BuildUri(baseUrl!, string.Format(allTimeTpl, Uri.EscapeDataString(clubName)));
            var allTimeJson = await _eaHttpClient.GetStringAsync(allTimeUri, ct);
            if (allTimeJson is not null)
            {
                List<SearchClubResult> allTimePayload;
                try { allTimePayload = JsonSerializer.Deserialize<List<SearchClubResult>>(allTimeJson, options) ?? new(); }
                catch { allTimePayload = new(); }

                var allTimeById = allTimePayload.FirstOrDefault(x => long.TryParse(x.clubId, out var id) && id == clubId);
                var allTimePick = allTimeById ?? allTimePayload.FirstOrDefault();
                if (allTimePick != null)
                    return ToNullableInt(allTimePick.currentDivision);
            }
        }

        return division;
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
        try { return JsonSerializer.Deserialize<MembersStatsResponse>(json, options); }
        catch { return null; }
    }

    private async Task<(int inserted, int updated)> UpsertPlayoffAchievementsAsync(long clubId, IEnumerable<PlayoffAchievement> items, CancellationToken ct)
    {
        int inserted = 0, updated = 0;
        var existing = await _clubRepository.GetPlayoffAchievementsForUpdateAsync(clubId, ct);
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
                await _clubRepository.UpdatePlayoffAchievementAsync(row);
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
                await _clubRepository.AddPlayoffAchievementAsync(entity, ct);
                inserted++;
            }
        }

        return (inserted, updated);
    }

    private async Task UpsertOverallAsync(long clubId, OverallStats src, CancellationToken ct)
    {
        var existing = await _clubRepository.GetOverallStatsByClubIdAsync(clubId, ct);
        if (existing is null)
        {
            await _clubRepository.AddOverallStatsAsync(MapToEntity(clubId, src), ct);
        }
        else
        {
            MapToEntity(clubId, src, existing);
            await _clubRepository.UpdateOverallStatsAsync(existing);
        }
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

    private static int EnrichPlayers(
        List<MatchPlayerEntity> rows,
        Dictionary<string, MemberStats> byName)
    {
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
        return updated;
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
