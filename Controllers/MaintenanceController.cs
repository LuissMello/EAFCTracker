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

    // =========================================================
    // 1) REFRESH OVERALL & PLAYOFFS (existente)
    // =========================================================

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

            // ----- Overall upsert -----
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

    // =========================================================
    // 2) NOVO: ATUALIZAR currentDivision via SearchClubsEndpoint
    // =========================================================

    /// <summary>
    /// Atualiza a divisão atual (currentDivision) do clube informado.
    /// Se o nome não for passado, tenta descobrir pelo nome mais recente em MatchClubs.Details.Name.
    /// </summary>
    [HttpPost("club/{clubId:long}/division/refresh")]
    public async Task<IActionResult> RefreshClubCurrentDivision(long clubId, [FromQuery] string? name, CancellationToken ct)
    {
        if (clubId <= 0) return BadRequest("Informe um clubId válido.");

        EnsureDefaultHeaders(_http);

        // Se não veio name, pega o mais recente do banco
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

        // Atualiza na última linha de detalhes (ou em todas, se preferir)
        var detailsRows = await _db.OverallStats
                                   .Where(mc => mc.ClubId == clubId)
                                   .Distinct()
                                   .ToListAsync(ct);

        if (detailsRows.Count == 0)
            return NotFound(new { message = "Nenhum ClubDetails encontrado para este clube." });

        foreach (var d in detailsRows)
            d.CurrentDivision = div.Value;

        _db.OverallStats.UpdateRange(detailsRows);
        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            clubId,
            clubName = name,
            currentDivision = div.Value,
            updatedRows = detailsRows.Count
        });
    }

    // =========================================================
    // 3) NOVO: ENRIQUECER MatchPlayers via MembersStatsEndpoint
    // =========================================================

    /// <summary>
    /// Busca /members/stats na EA e grava ProOverall, ProOverallStr, ProHeight, ProName
    /// em MatchPlayerEntity para TODOS os jogadores do clube informado,
    /// casando por Player.Playername (case-insensitive).
    /// </summary>
    [HttpPost("club/{clubId:long}/members/enrich")]
    public async Task<IActionResult> EnrichMatchPlayersWithMembers(long clubId, CancellationToken ct)
    {
        if (clubId <= 0) return BadRequest("Informe um clubId válido.");

        EnsureDefaultHeaders(_http);

        var members = await FetchMembersStatsAsync(clubId, ct);
        if (members is null || members.members.Count == 0)
            return NotFound(new { message = "Nenhum membro retornado pela EA para este clubId." });

        // index por nome (case-insensitive)
        var byName = members.members
                            .Where(m => !string.IsNullOrWhiteSpace(m.name))
                            .GroupBy(m => m.name.Trim(), StringComparer.OrdinalIgnoreCase)
                            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        // carrega todos MatchPlayers do clube (inclui Player p/ nome)
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

                // Atualiza apenas se mudou ou está vazio
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

        return Ok(new
        {
            clubId,
            totalMatchPlayers = rows.Count,
            updated
        });
    }

    // =========================================================
    // 4) NOVO: endpoint combinado (divisão + membros)
    // =========================================================

    /// <summary>
    /// Atualiza a divisão atual e enriquece match players do clube em uma só chamada.
    /// Querystring "name" opcional para forçar o nome do clube.
    /// </summary>
    [HttpPost("club/{clubId:long}/refresh-external")]
    public async Task<IActionResult> RefreshClubExternal(long clubId, [FromQuery] string? name, CancellationToken ct)
    {
        if (clubId <= 0) return BadRequest("Informe um clubId válido.");

        // faz as duas operações em sequência
        var divResult = await RefreshClubCurrentDivision(clubId, name, ct) as OkObjectResult;
        var enrichResult = await EnrichMatchPlayersWithMembers(clubId, ct) as OkObjectResult;

        return Ok(new
        {
            division = divResult?.Value,
            members = enrichResult?.Value
        });
    }

    [HttpPost("club/{clubId:long}/opponents/division/refresh")]
    public async Task<IActionResult> RefreshOpponentsCurrentDivision(long clubId, CancellationToken ct)
    {
        if (clubId <= 0) return BadRequest("Informe um clubId válido.");

        EnsureDefaultHeaders(_http);

        // Descobrir adversários: todos os MatchClubs que compartilham o mesmo MatchId,
        // exceto o próprio clubId.
        var opponents = await
            (from mc in _db.MatchClubs.AsNoTracking()
             join my in _db.MatchClubs.AsNoTracking() on mc.MatchId equals my.MatchId
             where my.ClubId == clubId && mc.ClubId != clubId
             select new
             {
                 OpponentId = mc.ClubId,
                 Name = mc.Details != null ? mc.Details.Name : null,
                 MatchClubId = mc.Id
             })
            .ToListAsync(ct);

        if (opponents.Count == 0)
            return Ok(new { clubId, opponentsFound = 0, updated = 0, detailsUpdated = 0 });

        // Consolidar por adversário (pegar o nome não-nulo mais recente disponível)
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
            // Se não tivermos nome em nenhuma partida, tenta buscar um nome em qualquer detalhe salvo.
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

            // Atualiza todas as linhas de detalhes desse adversário
            var detailsRows = await _db.OverallStats
                                       .Where(mc => mc.ClubId == opp.OpponentId)
                                       .Distinct()
                                       .ToListAsync(ct);

            if (detailsRows.Count == 0)
            {
                results.Add(new { opponentId = opp.OpponentId, name, currentDivision = div.Value, status = "no_details_rows" });
                continue;
            }

            foreach (var d in detailsRows)
                d.CurrentDivision = div.Value;

            _db.OverallStats.UpdateRange(detailsRows);
            detailsUpdated += detailsRows.Count;
            updated++;

            results.Add(new { opponentId = opp.OpponentId, name, currentDivision = div.Value, detailsAffected = detailsRows.Count });
        }

        if (_db.ChangeTracker.HasChanges())
            await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            clubId,
            opponentsFound = grouped.Count,
            updatedOpponents = updated,
            detailsUpdated,
            results
        });
    }

    // --------- FETCHERS EXISTENTES ---------

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

        var uri = new Uri($"{baseUrl!.TrimEnd('/')}/{string.Format(endpointTpl, clubId).TrimStart('/')}");

        var resp = await _http.GetAsync(uri, ct);
        if (!resp.IsSuccessStatusCode) return null;

        var json = await resp.Content.ReadAsStringAsync(ct);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        // Pode ser lista ou item único
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

    // --------- NOVOS FETCHERS ---------

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
        var baseUrl = _config["EAFCSettings:BaseUrl"] ?? "";
        var searchTpl = _config["EAFCSettings:SearchClubsEndpoint"]
                        ?? "/allTimeLeaderboard/search?platform=common-gen5&clubName={0}";
        var url = $"{baseUrl.TrimEnd('/')}/{string.Format(searchTpl, Uri.EscapeDataString(clubName)).TrimStart('/')}";

        var resp = await _http.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode) return null;

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        List<SearchClubResult> payload;

        try
        {
            payload = System.Text.Json.JsonSerializer.Deserialize<List<SearchClubResult>>(await resp.Content.ReadAsStringAsync(ct), options) ?? new();
        }
        catch
        {
            payload = new();
        }

        // tenta casar por clubId primeiro
        var byId = payload.FirstOrDefault(x => long.TryParse(x.clubId, out var id) && id == clubId);
        var pick = byId ?? payload.FirstOrDefault();
        if (pick == null) return null;

        return ToNullableInt(pick.currentDivision);
    }

    private async Task<MembersStatsResponse?> FetchMembersStatsAsync(long clubId, CancellationToken ct)
    {
        var baseUrl = _config["EAFCSettings:BaseUrl"] ?? "";
        var membersTpl = _config["EAFCSettings:MembersStatsEndpoint"]
                         ?? "/members/stats?platform=common-gen5&clubId={0}";
        var url = $"{baseUrl.TrimEnd('/')}/{string.Format(membersTpl, clubId).TrimStart('/')}";

        var resp = await _http.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode) return null;

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<MembersStatsResponse>(await resp.Content.ReadAsStringAsync(ct), options);
        }
        catch
        {
            return null;
        }
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

    private static int? ToNullableInt(string? s) => int.TryParse(s, out var v) ? v : (int?)null;
}
