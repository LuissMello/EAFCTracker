using EAFCMatchTracker.Application.Dtos;
using EAFCMatchTracker.Application.Interfaces.Services;
using EAFCMatchTracker.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using DomainMatchType = EAFCMatchTracker.Domain.Entities.MatchType;

namespace EAFCMatchTracker.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ClubsController : ControllerBase
{
    private const int MinOpponentPlayers = 2;
    private const int MaxOpponentPlayers = 11;

    private readonly IClubService _clubService;
    private readonly IMatchService _matchService;
    private readonly IPlayerService _playerService;
    private readonly IGoalAnalysisService _goalAnalysisService;
    private readonly ILogger<ClubsController> _logger;

    public ClubsController(
        IClubService clubService,
        IMatchService matchService,
        IPlayerService playerService,
        IGoalAnalysisService goalAnalysisService,
        ILogger<ClubsController> logger)
    {
        _clubService = clubService;
        _matchService = matchService;
        _playerService = playerService;
        _goalAnalysisService = goalAnalysisService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ClubListItemDto>>> GetAll(CancellationToken ct)
    {
        _logger.LogInformation("GetAll called");
        try
        {
            var clubs = await _clubService.GetAllAsync(ct);
            return Ok(clubs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetAll");
            return StatusCode(500, "Erro interno ao buscar clubes.");
        }
    }

    [HttpGet("{clubId:long}/players/attributes")]
    public async Task<ActionResult<List<PlayerAttributeSnapshotDto>>> GetClubPlayersAttributes(
        long clubId,
        [FromQuery] int count = 10,
        CancellationToken ct = default)
    {
        _logger.LogInformation("GetClubPlayersAttributes called for clubId={ClubId}, count={Count}", clubId, count);
        try
        {
            if (clubId <= 0) return BadRequest("Informe um clubId válido.");
            if (count <= 0) return BadRequest("O número de partidas deve ser maior que zero.");

            var result = await _playerService.GetClubPlayersAttributesAsync(clubId, count, ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetClubPlayersAttributes for clubId={ClubId}", clubId);
            return StatusCode(500, "Erro interno ao buscar atributos dos jogadores.");
        }
    }

    [HttpGet("{clubId:long}/players/aggregate")]
    public async Task<ActionResult<List<PlayerStatisticsDto>>> GetClubPlayersAggregate(
        long clubId,
        [FromQuery] int count = 10,
        [FromQuery] int? opponentCount = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("GetClubPlayersAggregate called for clubId={ClubId}, count={Count}, opponentCount={OpponentCount}", clubId, count, opponentCount);
        try
        {
            if (clubId <= 0) return BadRequest("Informe um clubId válido.");
            if (count <= 0) return BadRequest("O número de partidas deve ser maior que zero.");

            var result = await _playerService.GetClubPlayersAggregateAsync(clubId, count, opponentCount, ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetClubPlayersAggregate for clubId={ClubId}", clubId);
            return StatusCode(500, "Erro interno ao buscar agregados dos jogadores.");
        }
    }

    [HttpGet("{clubId:long}/overall")]
    public async Task<ActionResult<PagedResult<ClubOverallStatsDto>>> GetClubOverall(
        long clubId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        _logger.LogInformation("GetClubOverall called for clubId={ClubId} page={Page} pageSize={PageSize}", clubId, page, pageSize);
        try
        {
            if (clubId <= 0) return BadRequest("Informe um clubId válido.");
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 200) pageSize = 200;

            var result = await _clubService.GetOverallPagedAsync(clubId, page, pageSize, ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetClubOverall for clubId={ClubId}", clubId);
            return StatusCode(500, "Erro interno ao buscar estatísticas gerais.");
        }
    }

    [HttpGet("{clubId:long}/matches/overall")]
    public async Task<ActionResult<PagedResult<MatchWithOverallStatsDto>>> GetMatchesWithOverall(
        long clubId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken ct = default)
    {
        _logger.LogInformation("GetMatchesWithOverall called for clubId={ClubId} page={Page} pageSize={PageSize}", clubId, page, pageSize);
        try
        {
            if (clubId <= 0) return BadRequest("Informe um clubId válido.");
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            var result = await _clubService.GetMatchesWithOverallAsync(clubId, page, pageSize, ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetMatchesWithOverall for clubId={ClubId}", clubId);
            return StatusCode(500, "Erro interno ao buscar partidas com estatísticas gerais.");
        }
    }

    [HttpGet("{clubId:long}/matches/{matchId:long}/overall")]
    public async Task<ActionResult<ClubOverallStatsDto>> GetOverallForMatch(
        long clubId,
        long matchId,
        CancellationToken ct)
    {
        _logger.LogInformation("GetOverallForMatch called for clubId={ClubId} matchId={MatchId}", clubId, matchId);
        try
        {
            if (clubId <= 0) return BadRequest("Informe um clubId válido.");
            if (matchId <= 0) return BadRequest("Informe um matchId válido.");

            var result = await _clubService.GetOverallForMatchAsync(clubId, matchId, ct);
            if (result == null) return NotFound();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetOverallForMatch for clubId={ClubId} matchId={MatchId}", clubId, matchId);
            return StatusCode(500, "Erro interno ao buscar estatísticas gerais da partida.");
        }
    }

    [HttpGet("{clubId:long}/playoffs")]
    public async Task<ActionResult<List<ClubPlayoffAchievementDto>>> GetClubPlayoffs(long clubId, CancellationToken ct)
    {
        _logger.LogInformation("GetClubPlayoffs called for clubId={ClubId}", clubId);
        try
        {
            if (clubId <= 0) return BadRequest("Informe um clubId válido.");

            var result = await _clubService.GetPlayoffsAsync(clubId, ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetClubPlayoffs for clubId={ClubId}", clubId);
            return StatusCode(500, "Erro interno ao buscar dados de playoffs.");
        }
    }

    [HttpGet("{clubId:long}/matches/statistics")]
    public async Task<ActionResult<FullMatchStatisticsDto>> GetMatchStatistics(long clubId, CancellationToken ct)
    {
        _logger.LogInformation("GetMatchStatistics called for clubId={ClubId}", clubId);
        try
        {
            if (clubId <= 0) return BadRequest("Informe um clubId válido.");

            var result = await _matchService.GetMatchStatisticsAsync(clubId, ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetMatchStatistics for clubId={ClubId}", clubId);
            return StatusCode(500, "Erro interno ao buscar estatísticas das partidas.");
        }
    }

    [HttpGet("{clubId:long}/matches/statistics/limited")]
    public async Task<ActionResult<FullMatchStatisticsDto>> GetMatchStatisticsLimited(
        long clubId,
        [FromQuery] int? opponentCount,
        [FromQuery] int count = 10,
        CancellationToken ct = default)
    {
        _logger.LogInformation("GetMatchStatisticsLimited called for clubId={ClubId}, count={Count}, opponentCount={OpponentCount}", clubId, count, opponentCount);
        try
        {
            if (clubId <= 0) return BadRequest("Informe um clubId válido.");
            if (count <= 0) return BadRequest("O número de partidas deve ser maior que zero.");

            opponentCount = ReadOppAliasOrNull(Request, opponentCount);
            if (opponentCount.HasValue)
            {
                opponentCount = ClampOpp(opponentCount.Value);
                if (opponentCount is < MinOpponentPlayers or > MaxOpponentPlayers)
                    return BadRequest($"opponentCount deve estar entre {MinOpponentPlayers} e {MaxOpponentPlayers}.");
            }

            var result = await _matchService.GetMatchStatisticsLimitedAsync(clubId, count, opponentCount, ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetMatchStatisticsLimited for clubId={ClubId}", clubId);
            return StatusCode(500, "Erro interno ao buscar estatísticas limitadas das partidas.");
        }
    }

    [HttpGet("matches/statistics/by-date-range-grouped")]
    public async Task<ActionResult<List<FullMatchStatisticsByDayDto>>> GetMatchStatisticsByDateRangeGrouped_Multi(
        [FromQuery] string clubIds,
        [FromQuery] DateTime start,
        [FromQuery] DateTime end,
        [FromQuery] int? opponentCount,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "GetMatchStatisticsByDateRangeGrouped_Multi clubIds={ClubIds}, start={Start}, end={End}, opponentCount={OpponentCount}",
            clubIds, start, end, opponentCount);

        try
        {
            if (string.IsNullOrWhiteSpace(clubIds))
                return BadRequest("Informe 'clubIds' (ex.: 355651,352016).");
            if (start == default || end == default)
                return BadRequest("Informe 'start' e 'end' válidos (YYYY-MM-DD).");
            if (end < start)
                return BadRequest("'end' deve ser maior ou igual a 'start'.");

            var ids = clubIds
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => long.TryParse(s, out var v) ? (long?)v : null)
                .Where(v => v.HasValue && v.Value > 0)
                .Select(v => v!.Value)
                .Distinct()
                .ToList();

            if (ids.Count == 0)
                return BadRequest("Nenhum clubId válido em 'clubIds'.");

            bool applyOpponentFilter = opponentCount.HasValue && ids.Count == 1;
            if (applyOpponentFilter)
            {
                opponentCount = ClampOpp(opponentCount!.Value);
                if (opponentCount is < MinOpponentPlayers or > MaxOpponentPlayers)
                    return BadRequest($"opponentCount deve estar entre {MinOpponentPlayers} e {MaxOpponentPlayers}.");
            }

            var startUtc = DateTime.SpecifyKind(start.Date, DateTimeKind.Utc);
            var endExclusiveUtc = DateTime.SpecifyKind(end.Date.AddDays(1), DateTimeKind.Utc);

            var result = await _matchService.GetMatchStatisticsByDateRangeGroupedAsync(
                ids, startUtc, endExclusiveUtc, applyOpponentFilter ? opponentCount : null, ct);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetMatchStatisticsByDateRangeGrouped_Multi");
            return StatusCode(500, "Erro interno ao buscar estatísticas por período.");
        }
    }

    [HttpGet("matches/statistics/player/by-date-range-grouped")]
    public async Task<ActionResult<List<PlayerStatisticsByDayDto>>> GetPlayerMatchStatisticsByDateRangeGrouped(
        [FromQuery] long playerId,
        [FromQuery] string clubIds,
        [FromQuery] DateTime start,
        [FromQuery] DateTime end,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "GetPlayerMatchStatisticsByDateRangeGrouped playerId={PlayerId}, clubIds={ClubIds}, start={Start}, end={End}",
            playerId, clubIds, start, end);

        try
        {
            if (playerId <= 0) return BadRequest("Informe um playerId válido.");
            if (string.IsNullOrWhiteSpace(clubIds)) return BadRequest("Informe 'clubIds' (ex.: 355651,352016).");
            if (start == default || end == default) return BadRequest("Informe 'start' e 'end' válidos (YYYY-MM-DD).");
            if (end < start) return BadRequest("'end' deve ser maior ou igual a 'start'.");

            var ids = clubIds
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => long.TryParse(s, out var v) ? (long?)v : null)
                .Where(v => v.HasValue && v.Value > 0)
                .Select(v => v!.Value)
                .Distinct()
                .ToList();

            if (ids.Count == 0) return BadRequest("Nenhum clubId válido em 'clubIds'.");

            var startUtc = DateTime.SpecifyKind(start.Date, DateTimeKind.Utc);
            var endExclusiveUtc = DateTime.SpecifyKind(end.Date.AddDays(1), DateTimeKind.Utc);

            var result = await _matchService.GetPlayerMatchStatisticsByDateRangeGroupedAsync(playerId, ids, startUtc, endExclusiveUtc, ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetPlayerMatchStatisticsByDateRangeGrouped");
            return StatusCode(500, "Erro interno ao buscar estatísticas por jogador e período.");
        }
    }

    [HttpGet("matches/results")]
    public async Task<ActionResult<PagedResult<MatchResultDto>>> GetMultiClubMatchResults(
        [FromQuery] long[] clubIds,
        [FromQuery] DomainMatchType matchType = DomainMatchType.All,
        [FromQuery] int? opponentCount = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        _logger.LogInformation("GetMultiClubMatchResults called. ClubIds={ClubIds}, matchType={MatchType}, page={Page}, pageSize={PageSize}",
            string.Join(",", clubIds), matchType, page, pageSize);
        try
        {
            if (clubIds == null || clubIds.Length == 0)
                return BadRequest("Informe ao menos um clubId.");

            var ids = clubIds.Distinct().ToList();

            if (page < 1) page = 1;
            const int MaxPageSize = 200;
            if (pageSize < 1) pageSize = 50;
            if (pageSize > MaxPageSize) pageSize = MaxPageSize;

            opponentCount = ReadOppAliasOrNull(Request, opponentCount);
            if (opponentCount.HasValue)
            {
                opponentCount = ClampOpp(opponentCount.Value);
                if (opponentCount < MinOpponentPlayers || opponentCount > MaxOpponentPlayers)
                    return BadRequest($"opponentCount deve estar entre {MinOpponentPlayers} e {MaxOpponentPlayers}.");
            }

            var result = await _matchService.GetMultiClubMatchResultsAsync(ids, matchType, opponentCount, page, pageSize, ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetMultiClubMatchResults");
            return StatusCode(500, "Erro interno ao buscar resultados das partidas.");
        }
    }

    [HttpGet("{clubId:long}/matches/results")]
    public async Task<ActionResult<PagedResult<MatchResultDto>>> GetMatchResults(
        long clubId,
        [FromQuery] DomainMatchType matchType = DomainMatchType.All,
        [FromQuery] int? opponentCount = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        _logger.LogInformation("GetMatchResults called for clubId={ClubId}, matchType={MatchType}, opponentCount={OpponentCount}, page={Page}, pageSize={PageSize}",
            clubId, matchType, opponentCount, page, pageSize);
        try
        {
            if (clubId <= 0) return BadRequest("Informe um clubId válido.");

            if (page < 1) page = 1;
            const int MaxPageSize = 200;
            if (pageSize < 1) pageSize = 50;
            if (pageSize > MaxPageSize) pageSize = MaxPageSize;

            opponentCount = ReadOppAliasOrNull(Request, opponentCount);
            if (opponentCount.HasValue)
            {
                opponentCount = ClampOpp(opponentCount.Value);
                if (opponentCount < MinOpponentPlayers || opponentCount > MaxOpponentPlayers)
                    return BadRequest($"opponentCount deve estar entre {MinOpponentPlayers} e {MaxOpponentPlayers}.");
            }

            var result = await _matchService.GetMatchResultsAsync(clubId, matchType, opponentCount, page, pageSize, ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetMatchResults for clubId={ClubId}", clubId);
            return StatusCode(500, "Erro interno ao buscar resultados das partidas.");
        }
    }

    [HttpGet("records")]
    public async Task<ActionResult<ClubRecordsDto>> GetClubRecords(
        [FromQuery] string clubIds,
        CancellationToken ct = default)
    {
        _logger.LogInformation("GetClubRecords called for clubIds={ClubIds}", clubIds);
        try
        {
            if (string.IsNullOrWhiteSpace(clubIds))
                return BadRequest("Informe 'clubIds'.");

            var ids = clubIds
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => long.TryParse(s, out var v) ? (long?)v : null)
                .Where(v => v.HasValue && v.Value > 0)
                .Select(v => v!.Value)
                .Distinct()
                .ToList();

            if (ids.Count == 0) return BadRequest("Nenhum clubId válido em 'clubIds'.");

            var result = await _matchService.GetClubRecordsAsync(ids, ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetClubRecords for clubIds={ClubIds}", clubIds);
            return StatusCode(500, "Erro interno ao buscar recordes do clube.");
        }
    }

    [HttpGet("opponents")]
    public async Task<ActionResult<OpponentsAnalysisDto>> GetOpponentsAnalysis(
        [FromQuery] string clubIds,
        CancellationToken ct = default)
    {
        _logger.LogInformation("GetOpponentsAnalysis called for clubIds={ClubIds}", clubIds);
        try
        {
            if (string.IsNullOrWhiteSpace(clubIds))
                return BadRequest("Informe 'clubIds'.");

            var ids = clubIds
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => long.TryParse(s, out var v) ? (long?)v : null)
                .Where(v => v.HasValue && v.Value > 0)
                .Select(v => v!.Value)
                .Distinct()
                .ToList();

            if (ids.Count == 0) return BadRequest("Nenhum clubId válido em 'clubIds'.");

            var result = await _matchService.GetOpponentsAnalysisAsync(ids, ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetOpponentsAnalysis for clubIds={ClubIds}", clubIds);
            return StatusCode(500, "Erro interno ao buscar análise de adversários.");
        }
    }

    [HttpDelete("{clubId:long}/matches")]
    public async Task<IActionResult> DeleteMatchesByClub(long clubId, CancellationToken ct)
    {
        _logger.LogInformation("DeleteMatchesByClub called for clubId={ClubId}", clubId);
        try
        {
            await _matchService.DeleteMatchesByClubAsync(clubId, ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Clube não encontrado" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in DeleteMatchesByClub for clubId={ClubId}", clubId);
            return StatusCode(500, "Erro interno ao deletar partidas do clube.");
        }
    }

    [HttpGet("grouped/matches/statistics/limited")]
    public async Task<IActionResult> GetGroupedLimited(
        [FromQuery] string clubIds,
        [FromQuery] int count = 20,
        [FromQuery] int? opponentCount = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("GetGroupedLimited called for clubIds={ClubIds}, count={Count}, opponentCount={OpponentCount}", clubIds, count, opponentCount);
        try
        {
            if (string.IsNullOrWhiteSpace(clubIds))
                return BadRequest("clubIds required");

            var ids = clubIds
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => long.TryParse(s, out var x) ? x : (long?)null)
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .Distinct()
                .ToList();

            if (ids.Count == 0) return BadRequest("invalid clubIds");

            var result = await _matchService.GetGroupedLimitedAsync(ids, count, opponentCount, ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetGroupedLimited for clubIds={ClubIds}", clubIds);
            return StatusCode(500, "Erro interno ao buscar estatísticas agrupadas.");
        }
    }

    [HttpGet("{clubId:long}/goals/analysis")]
    public async Task<IActionResult> GetGoalAnalysis(
        long clubId,
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        CancellationToken ct)
    {
        _logger.LogInformation("GetGoalAnalysis clubId={ClubId} from={From} to={To}", clubId, from, to);
        try
        {
            var fromUtc = DateTime.SpecifyKind(from.Date, DateTimeKind.Utc);
            var toUtc = DateTime.SpecifyKind(to.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);

            var result = await _goalAnalysisService.GetGoalAnalysisAsync(clubId, fromUtc, toUtc, ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetGoalAnalysis clubId={ClubId}", clubId);
            return StatusCode(500, "Erro interno ao buscar análise de gols.");
        }
    }

    private static int ClampOpp(int value) => Math.Min(MaxOpponentPlayers, Math.Max(MinOpponentPlayers, value));

    private static int? ReadOppAliasOrNull(HttpRequest req, int? opponentCount)
    {
        if (opponentCount.HasValue) return opponentCount;
        return req.Query.TryGetValue("opp", out var v) && int.TryParse(v, out var parsed) ? parsed : null;
    }
}
