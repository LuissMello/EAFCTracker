using EAFCMatchTracker.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace EAFCMatchTracker.Services;

public class ClubMatchService : IClubMatchService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly EAFCContext _db;
    private readonly JsonSerializerOptions _jsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

    public ClubMatchService(HttpClient httpClient, IConfiguration config, EAFCContext db)
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
        };

        _httpClient = new HttpClient(handler, disposeHandler: true);
        _httpClient.Timeout = TimeSpan.FromSeconds(60);
        _config = config;
        _db = db;
    }

    public async Task FetchAndStoreMatchesAsync(string clubId, string matchType, CancellationToken ct)
    {
        var matches = await FetchMatches(clubId, matchType);
        if (matches.Count == 0) return;

        var idMap = matches
            .Select(m => new { Match = m, Ok = long.TryParse(m.MatchId, out var id), Id = long.TryParse(m.MatchId, out var id2) ? id2 : 0 })
            .Where(x => x.Ok)
            .GroupBy(x => x.Id)
            .ToDictionary(g => g.Key, g => g.First().Match);

        if (idMap.Count == 0) return;

        var allIds = idMap.Keys.ToList();

        var existingIds = await _db.Matches
            .AsNoTracking()
            .Where(m => allIds.Contains(m.MatchId))
            .Select(m => m.MatchId)
            .ToListAsync(ct);

        var existing = existingIds.Count > 0 ? existingIds.ToHashSet() : new HashSet<long>();

        var newMatches = allIds
            .Where(id => !existing.Contains(id))
            .Select(id => idMap[id])
            .ToList();

        if (newMatches.Count == 0) return;

        foreach (var match in newMatches)
        {
            await SaveMatchAsync(match, matchType, ct);
        }
    }

    private async Task<List<Match>> FetchMatches(string clubId, string matchType, CancellationToken ct = default)
    {
        var endpointTemplate = _config["EAFCSettings:ClubMatchesEndpoint"];
        var baseUrl = _config["EAFCSettings:BaseUrl"];

        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("EAFCSettings:BaseUrl não configurado.");
        if (string.IsNullOrWhiteSpace(endpointTemplate))
            throw new InvalidOperationException("EAFCSettings:ClubMatchesEndpoint não configurado.");


        var endpoint = new Uri(new Uri(baseUrl) + string.Format(endpointTemplate, clubId, matchType));

        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint)
        {
            Version = HttpVersion.Version20, // tenta HTTP/2 quando possível
            VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher
        };

        // Headers
        request.Headers.UserAgent.ParseAdd("PostmanRuntime/7.46.1");
        request.Headers.Connection.Clear();
        request.Headers.Connection.Add("keep-alive");
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.AcceptEncoding.ParseAdd("gzip, deflate, br");

        // Calcula Host corretamente (com porta se não for padrão)
        var hostHeader = endpoint.IsDefaultPort ? endpoint.IdnHost : $"{endpoint.IdnHost}:{endpoint.Port}";
        request.Headers.Host = hostHeader;

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        var matches = JsonSerializer.Deserialize<List<Match>>(json, _jsonOpts) ?? new List<Match>();
        return matches;
    }


    // =========================
    // DTOs mínimos p/ integrações externas
    // =========================
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

    // =========================
    // Helpers externos
    // =========================
    private async Task<int?> FetchCurrentDivisionByNameAsync(string clubName, long clubId, CancellationToken ct)
    {
        var baseUrl = _config["EAFCSettings:BaseUrl"] ?? "";
        var searchTpl = _config["EAFCSettings:SearchClubsEndpoint"] ?? "/allTimeLeaderboard/search?platform=common-gen5&clubName={0}";
        var url = $"{baseUrl.TrimEnd('/')}/{string.Format(searchTpl, Uri.EscapeDataString(clubName)).TrimStart('/')}";

        using var resp = await _httpClient.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode) return null;

        var payload = await resp.Content.ReadFromJsonAsync<List<SearchClubResult>>(_jsonOpts, ct) ?? new();

        // tenta casar por clubId primeiro
        var byId = payload.FirstOrDefault(x => long.TryParse(x.clubId, out var id) && id == clubId);
        var pick = byId ?? payload.FirstOrDefault();
        if (pick == null) return null;

        return ToNullableInt(pick.currentDivision);
    }

    private async Task<MembersStatsResponse?> FetchMembersStatsAsync(long clubId, CancellationToken ct)
    {
        var baseUrl = _config["EAFCSettings:BaseUrl"] ?? "";
        var membersTpl = _config["EAFCSettings:MembersStatsEndpoint"] ?? "/members/stats?platform=common-gen5&clubId={0}";
        var url = $"{baseUrl.TrimEnd('/')}/{string.Format(membersTpl, clubId).TrimStart('/')}";

        using var resp = await _httpClient.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode) return null;

        return await resp.Content.ReadFromJsonAsync<MembersStatsResponse>(_jsonOpts, ct);
    }

    private static int? ToNullableInt(string? s) => int.TryParse(s, out var v) ? v : (int?)null;

    // =========================
    // SaveMatchAsync com integrações externas
    // =========================
    private async Task SaveMatchAsync(Match match, string matchType, CancellationToken ct = default)
    {
        if (match == null || string.IsNullOrWhiteSpace(match.MatchId))
            throw new ArgumentException("Match inválido.");

        long matchId = Convert.ToInt64(match.MatchId);

        // -------- PRÉ-BUSCAS externas por clube (fora da transação) --------
        var preFetchedDivisions = new Dictionary<long, int?>();
        var preFetchedMembers = new Dictionary<long, MembersStatsResponse?>();

        if (match.Clubs != null && match.Clubs.Count > 0)
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("PostmanRuntime/7.36.0");
            _httpClient.DefaultRequestHeaders.Connection.ParseAdd("keep-alive");

            var tasks = match.Clubs.Select(async kv =>
            {
                var cid = long.Parse(kv.Key);
                var name = kv.Value?.Details?.Name?.Trim();

                // currentDivision via SearchClubsEndpoint (por nome)
                int? div = null;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    try { div = await FetchCurrentDivisionByNameAsync(name!, cid, ct); }
                    catch { /* tolerante a falhas externas */ }
                }
                preFetchedDivisions[cid] = div;

                // /members/stats por clubId
                try { preFetchedMembers[cid] = await FetchMembersStatsAsync(cid, ct); }
                catch { preFetchedMembers[cid] = null; }
            });

            await Task.WhenAll(tasks);
        }

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
                        SelectedKitType = club.Details?.CustomKit?.SelectedKitType,

                    };

                    // Mantém o comportamento original de buscar overall/playoffs.
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

                    // dicionário name -> MemberStats (case-insensitive) deste clube (se disponível)
                    var members = preFetchedMembers.TryGetValue(cid, out var ms) ? ms : null;
                    var byName = (members?.members ?? new List<MemberStats>())
                        .GroupBy(m => (m.name ?? "").Trim(), StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

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

                        // enriquecer com /members/stats (match por Playername)
                        MemberStats? mm = null;
                        var keyName = (p.Playername ?? "").Trim();
                        if (!string.IsNullOrEmpty(keyName))
                            byName.TryGetValue(keyName, out mm);

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
                            Rating = NormalizeRating(SafeDouble(data.Rating)),
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
                            Wins = SafeShort(data.Wins),
                            Archetypeid = SafeShort(data.Archetypeid),
                            BallDiveSaves = SafeShort(data.BallDiveSaves),
                            CrossSaves = SafeShort(data.CrossSaves),
                            GameTime = SafeShort(data.GameTime),
                            GoodDirectionSaves = SafeShort(data.GoodDirectionSaves),
                            MatchEventAggregate0 = data.MatchEventAggregate0,
                            MatchEventAggregate1 = data.MatchEventAggregate1,
                            MatchEventAggregate2 = data.MatchEventAggregate2,
                            MatchEventAggregate3 = data.MatchEventAggregate3,
                            ParrySaves = SafeShort(data.ParrySaves),
                            PunchSaves = SafeShort(data.PunchSaves),
                            ReflexSaves = SafeShort(data.ReflexSaves),
                            SecondsPlayed = SafeShort(data.SecondsPlayed),
                            UserResult = SafeShort(data.UserResult),

                            // ===== NOVOS CAMPOS DO MEMBERS/STATS =====
                            ProOverall = ToNullableInt(mm?.proOverall),
                            ProOverallStr = mm?.proOverallStr,
                            ProHeight = ToNullableInt(mm?.proHeight),
                            ProName = string.IsNullOrWhiteSpace(mm?.proName) ? null : mm!.proName
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

    private static double NormalizeRating(double rating)
    {
        if (double.IsNaN(rating) || double.IsInfinity(rating)) return 0d;

        var scaled = rating > 10 ? rating / 100d : rating;

        if (scaled < 0) scaled = 0;
        if (scaled > 10) scaled = 10;

        return Math.Round(scaled, 2, MidpointRounding.AwayFromZero);
    }

    // ===== OVERALL / PLAYOFFS (mantido) =====
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

            // OVERALL (comportamento original)
            if (overallResp.IsSuccessStatusCode)
            {
                var jsonOverall = await overallResp.Content.ReadAsStringAsync(ct);

                List<OverallStats>? list = null;
                try { list = JsonSerializer.Deserialize<List<OverallStats>>(jsonOverall, _jsonOpts); }
                catch
                {
                    var one = JsonSerializer.Deserialize<OverallStats>(jsonOverall, _jsonOpts);
                    if (one != null) list = new List<OverallStats> { one };
                }

                if (list != null && list.Count > 0)
                {
                    var src = list[0];
                    var existing = await _db.OverallStats.FirstOrDefaultAsync(x => x.ClubId == clubId, ct);

                    int? div = null;

                    try
                    {
                        var lastName = await _db.MatchClubs
                            .AsNoTracking()
                            .Where(mc => mc.ClubId == clubId && mc.Details != null && mc.Details.Name != null)
                            .OrderByDescending(mc => mc.Id)
                            .Select(mc => mc.Details!.Name!)
                            .FirstOrDefaultAsync(ct);

                        if (!string.IsNullOrWhiteSpace(lastName))
                        {
                            div = await FetchCurrentDivisionByNameAsync(lastName, clubId, ct);
                        }
                    }
                    catch
                    {
                        // silencioso
                    }

                    if (existing == null)
                    {
                        var entity = MapToEntity(clubId, src, div);
                        await _db.OverallStats.AddAsync(entity, ct);
                    }
                    else
                    {
                        MapToEntity(clubId, src, div, existing);
                        _db.OverallStats.Update(existing);
                    }

                    await _db.SaveChangesAsync(ct);
                }
            }

            // PLAYOFF ACHIEVEMENTS
            if (playoffsResp.IsSuccessStatusCode)
            {
                var jsonPlayoffs = await playoffsResp.Content.ReadAsStringAsync(ct);

                List<PlayoffAchievement>? items = null;
                try { items = JsonSerializer.Deserialize<List<PlayoffAchievement>>(jsonPlayoffs, _jsonOpts); }
                catch
                {
                    var one = JsonSerializer.Deserialize<PlayoffAchievement>(jsonPlayoffs, _jsonOpts);
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

    private static OverallStatsEntity MapToEntity(long clubId, OverallStats src, int? div, OverallStatsEntity? target = null)
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

        if (div.HasValue)
            o.CurrentDivision = div.Value;

        return o;
    }

    private static int TryParseSeasonAsNumber(string? seasonId)
    {
        if (string.IsNullOrWhiteSpace(seasonId)) return int.MinValue;
        return int.TryParse(seasonId, out var n) ? n : int.MinValue;
    }

    private async Task UpsertPlayoffAchievementsAsync(long clubId, IEnumerable<PlayoffAchievement> items, CancellationToken ct)
    {
        var existing = await _db.PlayoffAchievements
                                .Where(p => p.ClubId == clubId)
                                .ToListAsync(ct);

        var map = existing.ToDictionary(p => p.SeasonId, StringComparer.OrdinalIgnoreCase);

        var utcNow = DateTime.UtcNow;

        foreach (var it in items)
        {
            if (string.IsNullOrWhiteSpace(it.SeasonId))
                continue;

            if (map.TryGetValue(it.SeasonId, out var row))
            {
                row.SeasonName = it.SeasonName;
                row.BestDivision = it.BestDivision;
                row.BestFinishGroup = it.BestFinishGroup;
                row.UpdatedAtUtc = utcNow;

                _db.PlayoffAchievements.Update(row);
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
            }
        }

        await _db.SaveChangesAsync(ct);
    }
}
