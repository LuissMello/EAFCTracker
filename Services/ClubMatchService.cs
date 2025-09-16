using EAFCMatchTracker.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Linq;

namespace EAFCMatchTracker.Services;

public class ClubMatchService : IClubMatchService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly EAFCContext _db;

    public ClubMatchService(HttpClient httpClient, IConfiguration config, EAFCContext db)
    {
        _httpClient = httpClient;
        _config = config;
        _db = db;
    }

    public async Task FetchAndStoreMatchesAsync(string clubId, string matchType, CancellationToken ct)
    {
        List<Match> matches = await FetchMatches(clubId, matchType);

        foreach (Match match in matches)
        {
            await SaveMatchAsync(match, matchType, ct);
        }
    }

    private async Task<List<Match>> FetchMatches(string clubId, string matchType)
    {
        var endpointTemplate = _config["EAFCSettings:ClubMatchesEndpoint"];
        var baseUrl = _config["EAFCSettings:BaseUrl"];
        var endpoint = new Uri(baseUrl) + string.Format(endpointTemplate, clubId, matchType);

        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("PostmanRuntime/7.36.0");
        _httpClient.DefaultRequestHeaders.Connection.ParseAdd("keep-alive");

        var response = await _httpClient.GetAsync(endpoint);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();

        var matches = JsonSerializer.Deserialize<List<Match>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new List<Match>();

        return matches;
    }

    private async Task SaveMatchAsync(Match match, string matchType, CancellationToken ct = default)
    {
        if (match == null || string.IsNullOrWhiteSpace(match.MatchId))
            throw new ArgumentException("Match inválido.");

        long matchId = Convert.ToInt64(match.MatchId);

        if (await _db.Matches.AnyAsync(m => m.MatchId == matchId, ct))
            return;

        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            var matchEntity = new MatchEntity
            {
                MatchId = matchId,
                MatchType = matchType == "leagueMatch" ? MatchType.League : MatchType.Playoff,
                Timestamp = DateTimeOffset.FromUnixTimeSeconds(match.Timestamp).UtcDateTime,
            };
            await _db.Matches.AddAsync(matchEntity, ct);

            if (match.Clubs != null)
            {
                foreach (var entry in match.Clubs)
                {
                    long clubId = long.Parse(entry.Key);
                    var club = entry.Value;

                    var details = new ClubDetailsEntity
                    {
                        ClubId = clubId,
                        Name = club.Details?.Name,
                        RegionId = club.Details?.RegionId ?? 0,
                        TeamId = club.Details?.TeamId ?? 0,
                        StadName = club.Details?.CustomKit?.StadName,
                        KitId = club.Details?.CustomKit?.KitId,
                        CustomKitId = club.Details?.CustomKit?.CustomKitId,
                        CustomAwayKitId = club.Details?.CustomKit?.CustomAwayKitId,
                        CustomThirdKitId = club.Details?.CustomKit?.CustomThirdKitId,
                        CustomKeeperKitId = club.Details?.CustomKit?.CustomKeeperKitId,
                        KitColor1 = club.Details?.CustomKit?.KitColor1,
                        KitColor2 = club.Details?.CustomKit?.KitColor2,
                        KitColor3 = club.Details?.CustomKit?.KitColor3,
                        KitColor4 = club.Details?.CustomKit?.KitColor4,
                        KitAColor1 = club.Details?.CustomKit?.KitAColor1,
                        KitAColor2 = club.Details?.CustomKit?.KitAColor2,
                        KitAColor3 = club.Details?.CustomKit?.KitAColor3,
                        KitAColor4 = club.Details?.CustomKit?.KitAColor4,
                        KitThrdColor1 = club.Details?.CustomKit?.KitThrdColor1,
                        KitThrdColor2 = club.Details?.CustomKit?.KitThrdColor2,
                        KitThrdColor3 = club.Details?.CustomKit?.KitThrdColor3,
                        KitThrdColor4 = club.Details?.CustomKit?.KitThrdColor4,
                        DCustomKit = club.Details?.CustomKit?.DCustomKit,
                        CrestColor = club.Details?.CustomKit?.CrestColor,
                        CrestAssetId = club.Details?.CustomKit?.CrestAssetId,
                        SelectedKitType = club.Details?.CustomKit?.SelectedKitType
                    };

                    // >>> NOVO: buscar e persistir Overall do clube <<<
                    await FetchAndUpsertOverallStatsAsync(clubId, ct);

                    var mc = new MatchClubEntity
                    {
                        MatchId = matchId,
                        ClubId = clubId,
                        Date = DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(club.Date)).UtcDateTime,
                        GameNumber = Convert.ToInt32(club.GameNumber),
                        Goals = Convert.ToInt16(club.Goals),
                        GoalsAgainst = Convert.ToInt16(club.GoalsAgainst),
                        Losses = Convert.ToInt16(club.Losses),
                        MatchType = Convert.ToInt16(club.MatchType),
                        Result = Convert.ToInt16(club.Result),
                        Score = Convert.ToInt16(club.Score),
                        SeasonId = Convert.ToInt16(club.SeasonId),
                        Team = Convert.ToInt32(club.TEAM),
                        Ties = Convert.ToInt16(club.Ties),
                        Wins = Convert.ToInt16(club.Wins),
                        WinnerByDnf = club.WinnerByDnf == "1",
                        Details = details
                    };

                    await _db.MatchClubs.AddAsync(mc, ct);
                }
            }

            var playerKeys = match.Players?
                .SelectMany(club => club.Value.Select(p => (PlayerId: long.Parse(p.Key), ClubId: long.Parse(club.Key))))
                .Distinct()
                .ToList() ?? new();

            var playerIds = playerKeys.Select(k => k.PlayerId).Distinct().ToList();
            var clubIds = playerKeys.Select(k => k.ClubId).Distinct().ToList();

            var existingPlayers = await _db.Players
                .Where(p => playerIds.Contains(p.PlayerId) && clubIds.Contains(p.ClubId))
                .ToDictionaryAsync(p => (p.PlayerId, p.ClubId), ct);

            var toInsertPlayers = new List<PlayerEntity>();

            if (match.Players != null)
            {
                foreach (var clubEntry in match.Players)
                {
                    long cid = long.Parse(clubEntry.Key);

                    foreach (var playerEntry in clubEntry.Value)
                    {
                        long pid = long.Parse(playerEntry.Key);
                        var data = playerEntry.Value;
                        var key = (pid, cid);

                        if (!existingPlayers.TryGetValue(key, out var pe))
                        {
                            pe = new PlayerEntity
                            {
                                PlayerId = pid,
                                ClubId = cid,
                                Playername = data.Playername
                            };
                            toInsertPlayers.Add(pe);
                            existingPlayers[key] = pe;
                        }
                        else if (pe.Playername != data.Playername)
                        {
                            pe.Playername = data.Playername;
                        }
                    }
                }
            }

            var prevAuto = _db.ChangeTracker.AutoDetectChangesEnabled;
            _db.ChangeTracker.AutoDetectChangesEnabled = false;
            try
            {
                if (toInsertPlayers.Count > 0)
                    await _db.Players.AddRangeAsync(toInsertPlayers, ct);

                await _db.SaveChangesAsync(ct);
            }
            finally
            {
                _db.ChangeTracker.AutoDetectChangesEnabled = prevAuto;
            }

            var statsToInsert = new List<PlayerMatchStatsEntity>();
            var matchPlayerRows = new List<MatchPlayerEntity>();

            if (match.Players != null)
            {
                foreach (var clubEntry in match.Players)
                {
                    long cid = long.Parse(clubEntry.Key);

                    foreach (var playerEntry in clubEntry.Value)
                    {
                        long pid = long.Parse(playerEntry.Key);
                        var data = playerEntry.Value;
                        var p = existingPlayers[(pid, cid)];

                        var lastStats = await _db.PlayerMatchStats
                            .Where(s => s.PlayerEntityId == p.Id)
                            .OrderByDescending(s => s.Id)
                            .FirstOrDefaultAsync(ct);

                        long statsId;
                        var parsed = PlayerMatchStatsEntity.Parse(data.Vproattr);
                        if (lastStats == null || !parsed.IsEqualTo(lastStats))
                        {
                            parsed.PlayerEntityId = p.Id;
                            statsToInsert.Add(parsed);
                            statsId = 0;
                        }
                        else
                        {
                            statsId = lastStats.Id;
                        }

                        matchPlayerRows.Add(new MatchPlayerEntity
                        {
                            MatchId = matchId,
                            ClubId = cid,
                            PlayerEntityId = p.Id,
                            PlayerMatchStatsEntityId = statsId,
                            Assists = SafeShort(data.Assists),
                            Cleansheetsany = SafeShort(data.Cleansheetsany),
                            Cleansheetsdef = SafeShort(data.Cleansheetsdef),
                            Cleansheetsgk = SafeShort(data.Cleansheetsgk),
                            Goals = SafeShort(data.Goals),
                            Goalsconceded = SafeShort(data.Goalsconceded),
                            Losses = SafeShort(data.Losses),
                            Mom = data.Mom == "1",
                            Namespace = SafeShort(data.Namespace),
                            Passattempts = SafeShort(data.Passattempts),
                            Passesmade = SafeShort(data.Passesmade),
                            Pos = data.Pos ?? "",
                            Rating = SafeDouble(data.Rating),
                            Realtimegame = data.Realtimegame ?? "",
                            Realtimeidle = data.Realtimeidle ?? "",
                            Redcards = SafeShort(data.Redcards),
                            Saves = SafeShort(data.Saves),
                            Score = SafeShort(data.Score),
                            Shots = SafeShort(data.Shots),
                            Tackleattempts = SafeShort(data.Tackleattempts),
                            Tacklesmade = SafeShort(data.Tacklesmade),
                            Vproattr = data.Vproattr ?? "",
                            Vprohackreason = data.Vprohackreason ?? "",
                            Wins = SafeShort(data.Wins)
                        });
                    }
                }
            }

            if (statsToInsert.Count > 0)
            {
                await _db.PlayerMatchStats.AddRangeAsync(statsToInsert, ct);
                await _db.SaveChangesAsync(ct);

                var newPlayerIds = statsToInsert.Select(x => x.PlayerEntityId).Distinct().ToList();

                var latestByPlayer = await _db.PlayerMatchStats
                    .Where(s => newPlayerIds.Contains(s.PlayerEntityId))
                    .GroupBy(s => s.PlayerEntityId)
                    .Select(g => g.OrderByDescending(s => s.Id).First())
                    .ToDictionaryAsync(s => s.PlayerEntityId, ct);

                foreach (var row in matchPlayerRows.Where(r => r.PlayerMatchStatsEntityId == 0))
                    row.PlayerMatchStatsEntityId = latestByPlayer[row.PlayerEntityId].Id;

                foreach (var kv in latestByPlayer)
                {
                    var playerId = kv.Key;
                    var stats = kv.Value;
                    var player = await _db.Players.FirstAsync(p => p.Id == playerId, ct);
                    player.PlayerMatchStatsId = stats.Id;
                    _db.Players.Update(player);
                }

                await _db.SaveChangesAsync(ct);
            }

            matchPlayerRows = matchPlayerRows
                .DistinctBy(r => new { r.MatchId, r.ClubId, r.PlayerEntityId })
                .ToList();

            await _db.MatchPlayers.AddRangeAsync(matchPlayerRows, ct);

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
    }

    private static short SafeShort(object value)
        => short.TryParse(Convert.ToString(value), out var s) ? s : (short)0;

    private static double SafeDouble(object value)
        => double.TryParse(Convert.ToString(value), out var d) ? d : 0.0;

    // ===== NOVO: Overall =====

    private async Task FetchAndUpsertOverallStatsAsync(long clubId, CancellationToken ct)
    {
        try
        {
            var baseUrl = _config["EAFCSettings:BaseUrl"] ?? "";
            var overallTpl = _config["EAFCSettings:OverallStatsEndpoint"]
                             ?? _config["OverallStatsEndpoint"]
                             ?? "/clubs/overallStats?platform=common-gen5&clubIds={0}";

            var playoffsTpl = _config["EAFCSettings:PlayoffAchievementsEndpoint"]
                              ?? _config["PlayoffAchievementsEndpoint"]
                              ?? "/club/playoffAchievements?platform=common-gen5&clubId={0}";

            var overallUri = new Uri($"{baseUrl.TrimEnd('/')}/{string.Format(overallTpl, clubId).TrimStart('/')}");
            var playoffsUri = new Uri($"{baseUrl.TrimEnd('/')}/{string.Format(playoffsTpl, clubId).TrimStart('/')}");

            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("PostmanRuntime/7.36.0");
            _httpClient.DefaultRequestHeaders.Connection.ParseAdd("keep-alive");

            // Buscar em paralelo
            var overallTask = _httpClient.GetAsync(overallUri, ct);
            var playoffsTask = _httpClient.GetAsync(playoffsUri, ct);

            await Task.WhenAll(overallTask, playoffsTask);

            using var overallResp = overallTask.Result;
            using var playoffsResp = playoffsTask.Result;

            // ---- OVERALL (comportamento original) ----
            if (overallResp.IsSuccessStatusCode)
            {
                var jsonOverall = await overallResp.Content.ReadAsStringAsync(ct);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                List<OverallStats>? list = null;
                try
                {
                    list = JsonSerializer.Deserialize<List<OverallStats>>(jsonOverall, options);
                }
                catch
                {
                    var one = JsonSerializer.Deserialize<OverallStats>(jsonOverall, options);
                    if (one != null) list = new List<OverallStats> { one };
                }

                if (list != null && list.Count > 0)
                {
                    var src = list[0];
                    var existing = await _db.OverallStats.FirstOrDefaultAsync(x => x.ClubId == clubId, ct);

                    if (existing == null)
                    {
                        var entity = MapToEntity(clubId, src);
                        await _db.OverallStats.AddAsync(entity, ct);
                    }
                    else
                    {
                        MapToEntity(clubId, src, existing);
                        _db.OverallStats.Update(existing);
                    }

                    await _db.SaveChangesAsync(ct);
                }
            }

            // ---- PLAYOFF ACHIEVEMENTS (nova tabela 1:N) ----
            if (playoffsResp.IsSuccessStatusCode)
            {
                var jsonPlayoffs = await playoffsResp.Content.ReadAsStringAsync(ct);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                // Pode vir lista ou item único
                List<PlayoffAchievement>? items = null;
                try
                {
                    items = JsonSerializer.Deserialize<List<PlayoffAchievement>>(jsonPlayoffs, options);
                }
                catch
                {
                    var one = JsonSerializer.Deserialize<PlayoffAchievement>(jsonPlayoffs, options);
                    if (one != null) items = new List<PlayoffAchievement> { one };
                }

                if (items != null && items.Count > 0)
                {
                    await UpsertPlayoffAchievementsAsync(clubId, items, ct);
                }
            }
        }
        catch
        {
            // Silencioso por robustez
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
    private static int TryParseSeasonAsNumber(string? seasonId)
    {
        if (string.IsNullOrWhiteSpace(seasonId)) return int.MinValue;
        return int.TryParse(seasonId, out var n) ? n : int.MinValue;
    }

    private async Task UpsertPlayoffAchievementsAsync(long clubId, IEnumerable<PlayoffAchievement> items, CancellationToken ct)
    {
        // Carrega existentes do clube e indexa por SeasonId (case-insensitive)
        var existing = await _db.PlayoffAchievements
                                .Where(p => p.ClubId == clubId)
                                .ToListAsync(ct);

        var map = existing.ToDictionary(p => p.SeasonId, StringComparer.OrdinalIgnoreCase);

        var utcNow = DateTime.UtcNow;

        foreach (var it in items)
        {
            if (string.IsNullOrWhiteSpace(it.SeasonId))
                continue; // precisa de SeasonId para unicidade

            if (map.TryGetValue(it.SeasonId, out var row))
            {
                // UPDATE
                row.SeasonName = it.SeasonName;
                row.BestDivision = it.BestDivision;
                row.BestFinishGroup = it.BestFinishGroup;
                row.UpdatedAtUtc = utcNow;

                _db.PlayoffAchievements.Update(row);
            }
            else
            {
                // INSERT
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
            }
        }

        await _db.SaveChangesAsync(ct);
    }
}
