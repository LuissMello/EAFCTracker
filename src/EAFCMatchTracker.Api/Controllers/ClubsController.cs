using EAFCMatchTracker.Application.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EAFCMatchTracker.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ClubsController : ControllerBase
{
    private const int MinOpponentPlayers = 2;
    private const int MaxOpponentPlayers = 11;

    private readonly EAFCContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<ClubsController> _logger;

    public ClubsController(EAFCContext db, IConfiguration config, ILogger<ClubsController> logger)
    {
        _db = db;
        _config = config;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ClubListItemDto>>> GetAll()
    {
        _logger.LogInformation("GetAll called");
        try
        {
            var ids = ParseClubIdsFromConfig(_config);
            if (ids.Count == 0) return Ok(Array.Empty<ClubListItemDto>());

            var raw = await _db.MatchClubs
                .AsNoTracking()
                .Where(mc => ids.Contains(mc.ClubId))
                .GroupBy(mc => mc.ClubId)
                .Select(g => new
                {
                    ClubId = g.Key,
                    Name = g.OrderByDescending(x => x.Match.Timestamp)
                             .Select(x => x.Details.Name)
                             .FirstOrDefault(),
                    Team = g.OrderByDescending(x => x.Match.Timestamp)
                             .Select(x => x.Team)
                             .FirstOrDefault()
                })
                .OrderBy(x => x.Name)
                .ToListAsync();

            var clubs = raw.Select(c => new ClubListItemDto
            {
                ClubId = c.ClubId,
                Name = c.Name ?? $"Clube {c.ClubId}",
                CrestAssetId = c.Team.ToString()
            }).ToList();

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

            var matches = await _db.Matches
                .AsNoTracking()
                .Where(m => m.Clubs.Any(c => c.ClubId == clubId))
                .Include(m => m.MatchPlayers.Where(mp => mp.ClubId == clubId))
                    .ThenInclude(mp => mp.Player)
                .Include(m => m.MatchPlayers.Where(mp => mp.ClubId == clubId))
                    .ThenInclude(mp => mp.PlayerMatchStats)
                .OrderByDescending(m => m.Timestamp)
                .Take(count)
                .ToListAsync(ct);

            if (matches.Count == 0) return Ok(new List<PlayerAttributeSnapshotDto>());

            var byPlayer = matches
                .SelectMany(m => m.MatchPlayers.Select(mp => new
                {
                    m.MatchId,
                    m.Timestamp,
                    mp.PlayerEntityId,
                    mp.Player,
                    mp.PlayerMatchStats,
                    mp.Pos
                }))
                .Where(x => x.Player != null)
                .GroupBy(x => x.PlayerEntityId)
                .Select(g =>
                {
                    var latest = g.OrderByDescending(x => x.Timestamp).First();
                    return new PlayerAttributeSnapshotDto
                    {
                        Pos = latest.Pos,
                        PlayerId = latest.Player!.PlayerId,
                        PlayerName = latest.Player!.Playername ?? $"Player {latest.Player!.PlayerId}",
                        ClubId = latest.Player!.ClubId,
                        Statistics = latest.PlayerMatchStats is null
                            ? null
                            : new PlayerMatchStatsDto
                            {
                                Aceleracao = latest.PlayerMatchStats.Aceleracao,
                                Pique = latest.PlayerMatchStats.Pique,
                                Finalizacao = latest.PlayerMatchStats.Finalizacao,
                                Falta = latest.PlayerMatchStats.Falta,
                                Cabeceio = latest.PlayerMatchStats.Cabeceio,
                                ForcaDoChute = latest.PlayerMatchStats.ForcaDoChute,
                                ChuteLonge = latest.PlayerMatchStats.ChuteLonge,
                                Voleio = latest.PlayerMatchStats.Voleio,
                                Penalti = latest.PlayerMatchStats.Penalti,
                                Visao = latest.PlayerMatchStats.Visao,
                                Cruzamento = latest.PlayerMatchStats.Cruzamento,
                                Lancamento = latest.PlayerMatchStats.Lancamento,
                                PasseCurto = latest.PlayerMatchStats.PasseCurto,
                                Curva = latest.PlayerMatchStats.Curva,
                                Agilidade = latest.PlayerMatchStats.Agilidade,
                                Equilibrio = latest.PlayerMatchStats.Equilibrio,
                                PosAtaqueInutil = latest.PlayerMatchStats.PosAtaqueInutil,
                                ControleBola = latest.PlayerMatchStats.ControleBola,
                                Conducao = latest.PlayerMatchStats.Conducao,
                                Interceptacaos = latest.PlayerMatchStats.Interceptacaos,
                                NocaoDefensiva = latest.PlayerMatchStats.NocaoDefensiva,
                                DivididaEmPe = latest.PlayerMatchStats.DivididaEmPe,
                                Carrinho = latest.PlayerMatchStats.Carrinho,
                                Impulsao = latest.PlayerMatchStats.Impulsao,
                                Folego = latest.PlayerMatchStats.Folego,
                                Forca = latest.PlayerMatchStats.Forca,
                                Reacao = latest.PlayerMatchStats.Reacao,
                                Combatividade = latest.PlayerMatchStats.Combatividade,
                                Frieza = latest.PlayerMatchStats.Frieza,
                                ElasticidadeGL = latest.PlayerMatchStats.ElasticidadeGL,
                                ManejoGL = latest.PlayerMatchStats.ManejoGL,
                                ChuteGL = latest.PlayerMatchStats.ChuteGL,
                                ReflexosGL = latest.PlayerMatchStats.ReflexosGL,
                                PosGL = latest.PlayerMatchStats.PosGL
                            }
                    };
                })
                .OrderBy(p => p.PlayerName)
                .ToList();

            return Ok(byPlayer);
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

            var query = BaseClubMatchesQuery(clubId);
            query = ApplyOpponentFilter(query, clubId, opponentCount);

            var matches = await query
                .OrderByDescending(m => m.Timestamp)
                .Take(count)
                .ToListAsync(ct);

            if (matches.Count == 0)
                return Ok(new List<PlayerStatisticsDto>());

            var (_, players, _) = StatsAggregator.BuildLimitedForClub(clubId, matches);
            return Ok(players);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetClubPlayersAggregate for clubId={ClubId}", clubId);
            return StatusCode(500, "Erro interno ao buscar agregados dos jogadores.");
        }
    }

    [HttpGet("{clubId:long}/overall")]
    public async Task<ActionResult<List<ClubOverallStatsDto>>> GetClubOverall(long clubId, CancellationToken ct)
    {
        _logger.LogInformation("GetClubOverall called for clubId={ClubId}", clubId);

        try
        {
            if (clubId <= 0)
                return BadRequest("Informe um clubId válido.");

            var overallEntities = await _db.OverallStats
                .AsNoTracking()
                .Where(o => o.ClubId == clubId)
                .ToListAsync(ct);

            var clubsOverall = StatsAggregator.BuildClubsOverall(overallEntities);

            return Ok(clubsOverall);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetClubOverall for clubId={ClubId}", clubId);
            return StatusCode(500, "Erro interno ao buscar estatísticas gerais.");
        }
    }

    [HttpGet("{clubId:long}/playoffs")]
    public async Task<ActionResult<List<ClubPlayoffAchievementDto>>> GetClubPlayoffs(long clubId, CancellationToken ct)
    {
        _logger.LogInformation("GetClubPlayoffs called for clubId={ClubId}", clubId);

        try
        {
            if (clubId <= 0)
                return BadRequest("Informe um clubId válido.");

            var playoffEntities = await _db.PlayoffAchievements
                .AsNoTracking()
                .Where(p => p.ClubId == clubId)
                .ToListAsync(ct);

            var clubsPlayoffAchievements = StatsAggregator.BuildClubsPlayoffAchievements(playoffEntities);

            return Ok(clubsPlayoffAchievements);
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

            var matches = await BaseClubMatchesQuery(clubId)
                .OrderByDescending(m => m.Timestamp)
                .ToListAsync(ct);

            var allPlayers = matches
                .SelectMany(m => m.MatchPlayers)
                .Where(e => e.Player.ClubId == clubId)
                .ToList();

            if (allPlayers.Count == 0)
                return Ok(new FullMatchStatisticsDto());

            var clubsById = matches
                .SelectMany(m => m.Clubs)
                .GroupBy(c => c.ClubId)
                .ToDictionary(g => g.Key, g => g.First());

            var playersStats = StatsAggregator.BuildPerPlayer(allPlayers);
            var clubsStats = StatsAggregator.BuildPerClub(allPlayers, clubsById);

            var totalRows = allPlayers.Count;
            var totalGoals = playersStats.Sum(p => p.TotalGoals);
            var totalAssists = playersStats.Sum(p => p.TotalAssists);
            var totalPreAssists = playersStats.Sum(p => p.TotalPreAssists);
            var totalShots = playersStats.Sum(p => p.TotalShots);
            var totalPassesMade = playersStats.Sum(p => p.TotalPassesMade);
            var totalPassAttempts = playersStats.Sum(p => p.TotalPassAttempts);
            var totalTacklesMade = playersStats.Sum(p => p.TotalTacklesMade);
            var totalTackleAttempts = playersStats.Sum(p => p.TotalTackleAttempts);
            var totalRating = allPlayers.Sum(p => p.Rating);
            var totalWins = playersStats.Sum(p => p.TotalWins);
            var totalLosses = playersStats.Sum(p => p.TotalLosses);
            var totalCleanSheets = playersStats.Sum(p => p.TotalCleanSheets);
            var totalRedCards = playersStats.Sum(p => p.TotalRedCards);
            var totalSaves = playersStats.Sum(p => p.TotalSaves);
            var totalMom = playersStats.Sum(p => p.TotalMom);
            var totalDraws = playersStats.Sum(p => p.TotalDraws);
            var distinctPlayers = playersStats.Count;

            var overall = new MatchStatisticsDto
            {
                TotalMatches = matches.Count,
                TotalPlayers = distinctPlayers,
                TotalGoals = totalGoals,
                TotalAssists = totalAssists,
                TotalPreAssists = totalPreAssists,
                TotalShots = totalShots,
                TotalPassesMade = totalPassesMade,
                TotalPassAttempts = totalPassAttempts,
                TotalTacklesMade = totalTacklesMade,
                TotalTackleAttempts = totalTackleAttempts,
                TotalRating = totalRating,
                TotalWins = totalWins,
                TotalLosses = totalLosses,
                TotalDraws = totalDraws,
                TotalCleanSheets = totalCleanSheets,
                TotalRedCards = totalRedCards,
                TotalSaves = totalSaves,
                TotalMom = totalMom,
                AvgGoals = totalRows > 0 ? totalGoals / (double)totalRows : 0,
                AvgAssists = totalRows > 0 ? totalAssists / (double)totalRows : 0,
                AvgPreAssists = totalRows > 0 ? totalPreAssists / (double)totalRows : 0,
                AvgShots = totalRows > 0 ? totalShots / (double)totalRows : 0,
                AvgPassesMade = totalRows > 0 ? totalPassesMade / (double)totalRows : 0,
                AvgPassAttempts = totalRows > 0 ? totalPassAttempts / (double)totalRows : 0,
                AvgTacklesMade = totalRows > 0 ? totalTacklesMade / (double)totalRows : 0,
                AvgTackleAttempts = totalRows > 0 ? totalTackleAttempts / (double)totalRows : 0,
                AvgRating = totalRows > 0 ? totalRating / totalRows : 0,
                AvgRedCards = totalRows > 0 ? totalRedCards / (double)totalRows : 0,
                AvgSaves = totalRows > 0 ? totalSaves / (double)totalRows : 0,
                AvgMom = totalRows > 0 ? totalMom / (double)totalRows : 0,
                WinPercent = totalRows > 0 ? totalWins * 100.0 / totalRows : 0,
                LossPercent = totalRows > 0 ? totalLosses * 100.0 / totalRows : 0,
                DrawPercent = totalRows > 0 ? totalDraws * 100.0 / totalRows : 0,
                CleanSheetsPercent = totalRows > 0 ? totalCleanSheets * 100.0 / totalRows : 0,
                MomPercent = totalRows > 0 ? totalMom * 100.0 / totalRows : 0,
                PassAccuracyPercent = totalPassAttempts > 0 ? totalPassesMade * 100.0 / totalPassAttempts : 0,
                TackleSuccessPercent = totalTackleAttempts > 0 ? totalTacklesMade * 100.0 / totalTackleAttempts : 0,
                GoalAccuracyPercent = totalShots > 0 ? totalGoals * 100.0 / totalShots : 0
            };

            return Ok(new FullMatchStatisticsDto { Overall = overall, Players = playersStats, Clubs = clubsStats });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetMatchStatistics for clubId={ClubId}", clubId);
            return StatusCode(500, "Erro interno ao buscar estatísticas das partidas.");
        }
    }

    [HttpGet("{clubId:long}/matches/statistics/limited")]
    public async Task<ActionResult<FullMatchStatisticsDto>> GetMatchStatisticsLimited(long clubId, [FromQuery] int? opponentCount, [FromQuery] int count = 10, CancellationToken ct = default)
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

            var query = BaseClubMatchesQuery(clubId);
            query = ApplyOpponentFilter(query, clubId, opponentCount);

            var matches = await query.OrderByDescending(m => m.Timestamp).Take(count).ToListAsync(ct);
            if (matches.Count == 0) return Ok(new FullMatchStatisticsDto());

            var (overall, players, clubs) = StatsAggregator.BuildLimitedForClub(clubId, matches);
            if (players.Count == 0) return Ok(new FullMatchStatisticsDto());

            return Ok(new FullMatchStatisticsDto { Overall = overall, Players = players, Clubs = clubs });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetMatchStatisticsLimited for clubId={ClubId}", clubId);
            return StatusCode(500, "Erro interno ao buscar estatísticas limitadas das partidas.");
        }
    }

    // GET /api/Clubs/matches/statistics/by-date-range-grouped?clubIds=355651,352016&start=2025-10-01&end=2025-10-31
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

            var q = _db.Matches
                .AsNoTracking()
                .Include(m => m.Clubs).ThenInclude(c => c.Details)
                .Include(m => m.MatchPlayers).ThenInclude(mp => mp.Player)
                .Where(m => m.Clubs.Any(c => ids.Contains(c.ClubId)))
                .Where(m => m.Timestamp >= startUtc && m.Timestamp < endExclusiveUtc);

            if (applyOpponentFilter)
            {
                q = ApplyOpponentFilter(q, ids[0], opponentCount);
            }

            var matches = await q.OrderBy(m => m.Timestamp).ToListAsync(ct);
            if (matches.Count == 0)
                return Ok(Array.Empty<FullMatchStatisticsByDayDto>());

            var grouped = matches
    .GroupBy(m => m.Timestamp.Date)
    .Select(g =>
    {
        var dayMatches = g.ToList();

        var dayPlayers = dayMatches
            .SelectMany(m => m.MatchPlayers)
            .Where(mp => ids.Contains(mp.ClubId))
            .ToList();

        var playerStats = StatsAggregator.BuildPerPlayerMergedByGlobalId(dayPlayers);
        var clubStats = StatsAggregator.BuildSingleClubFromPlayers(dayPlayers, "Clubes agrupados");

        var (gf, ga) = ComputeGoalsForAgainst(dayMatches, ids);
        var (wins, draws, losses, countedMatches) = ComputeWinsDrawsLosses(dayMatches, ids);

        var totalRows = dayPlayers.Count;
        var totalGoals = playerStats.Sum(p => p.TotalGoals);
        var totalAssists = playerStats.Sum(p => p.TotalAssists);
        var totalPreAssists = playerStats.Sum(p => p.TotalPreAssists);
        var totalShots = playerStats.Sum(p => p.TotalShots);
        var totalPassesMade = playerStats.Sum(p => p.TotalPassesMade);
        var totalPassAttempts = playerStats.Sum(p => p.TotalPassAttempts);
        var totalTacklesMade = playerStats.Sum(p => p.TotalTacklesMade);
        var totalTackleAttempts = playerStats.Sum(p => p.TotalTackleAttempts);
        var totalRating = dayPlayers.Sum(p => p.Rating);
        var totalCleanSheets = playerStats.Sum(p => p.TotalCleanSheets);
        var totalRedCards = playerStats.Sum(p => p.TotalRedCards);
        var totalSaves = playerStats.Sum(p => p.TotalSaves);
        var totalMom = playerStats.Sum(p => p.TotalMom);
        var distinctPlayers = playerStats.Count;

        var overall = new MatchStatisticsDto
        {
            TotalMatches = countedMatches,   // ou dayMatches.Count, se preferir contar também jogos "grupo x grupo"
            TotalPlayers = distinctPlayers,

            TotalGoals = totalGoals,
            TotalAssists = totalAssists,
            TotalPreAssists = totalPreAssists,
            TotalShots = totalShots,
            TotalPassesMade = totalPassesMade,
            TotalPassAttempts = totalPassAttempts,
            TotalTacklesMade = totalTacklesMade,
            TotalTackleAttempts = totalTackleAttempts,
            TotalRating = totalRating,

            // ✅ estes 3 agora NÃO somam por jogador
            TotalWins = wins,
            TotalLosses = losses,
            TotalDraws = draws,

            TotalCleanSheets = totalCleanSheets,
            TotalRedCards = totalRedCards,
            TotalSaves = totalSaves,
            TotalMom = totalMom,

            AvgGoals = totalRows > 0 ? totalGoals / (double)totalRows : 0,
            AvgAssists = totalRows > 0 ? totalAssists / (double)totalRows : 0,
            AvgPreAssists = totalRows > 0 ? totalPreAssists / (double)totalRows : 0,
            AvgShots = totalRows > 0 ? totalShots / (double)totalRows : 0,
            AvgPassesMade = totalRows > 0 ? totalPassesMade / (double)totalRows : 0,
            AvgPassAttempts = totalRows > 0 ? totalPassAttempts / (double)totalRows : 0,
            AvgTacklesMade = totalRows > 0 ? totalTacklesMade / (double)totalRows : 0,
            AvgTackleAttempts = totalRows > 0 ? totalTackleAttempts / (double)totalRows : 0,
            AvgRating = totalRows > 0 ? totalRating / totalRows : 0,
            AvgRedCards = totalRows > 0 ? totalRedCards / (double)totalRows : 0,
            AvgSaves = totalRows > 0 ? totalSaves / (double)totalRows : 0,
            AvgMom = totalRows > 0 ? totalMom / (double)totalRows : 0,
            WinPercent = countedMatches > 0 ? wins * 100.0 / countedMatches : 0,
            LossPercent = countedMatches > 0 ? losses * 100.0 / countedMatches : 0,
            DrawPercent = countedMatches > 0 ? draws * 100.0 / countedMatches : 0,
            PassAccuracyPercent = totalPassAttempts > 0 ? totalPassesMade * 100.0 / totalPassAttempts : 0,
            TackleSuccessPercent = totalTackleAttempts > 0 ? totalTacklesMade * 100.0 / totalTackleAttempts : 0,
            GoalAccuracyPercent = totalShots > 0 ? totalGoals * 100.0 / totalShots : 0
        };

        // opcional: preencher GF/GA no clube agregado do dia
        clubStats.GoalsFor = gf;
        clubStats.GoalsAgainst = ga;

        return new FullMatchStatisticsByDayDto
        {
            Date = DateOnly.FromDateTime(g.Key),
            Statistics = new FullMatchStatisticsDto
            {
                Overall = overall,
                Players = playerStats,
                Clubs = new[] { clubStats }.ToList()
            }
        };
    })
    .OrderBy(x => x.Date)
    .ToList();

            return Ok(grouped);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetMatchStatisticsByDateRangeGrouped_Multi");
            return StatusCode(500, "Erro interno ao buscar estatísticas por período.");
        }
    }

    private static (int gf, int ga) ComputeGoalsForAgainst(IEnumerable<MatchEntity> matches, IReadOnlyCollection<long> ids)
    {
        int gf = 0, ga = 0;

        foreach (var m in matches)
        {
            // gols feitos pelos clubes selecionados
            var ourGoals = m.Clubs.Where(c => ids.Contains(c.ClubId)).Sum(c => c.Goals);
            // gols sofridos (gols dos adversários)
            var oppGoals = m.Clubs.Where(c => !ids.Contains(c.ClubId)).Sum(c => c.Goals);

            gf += ourGoals;
            ga += oppGoals;
        }

        return (gf, ga);
    }

    private static (int wins, int draws, int losses, int counted) ComputeWinsDrawsLosses(IEnumerable<MatchEntity> matches, IReadOnlyCollection<long> ids)
    {
        int wins = 0, draws = 0, losses = 0, counted = 0;

        foreach (var m in matches)
        {
            // Só conta partidas onde exatamente 1 clube do grupo participou (evita contar "grupo x grupo")
            int ourTeamsInThisMatch = m.Clubs.Count(c => ids.Contains(c.ClubId));
            if (ourTeamsInThisMatch != 1)
                continue;

            int ourGoals = m.Clubs.Where(c => ids.Contains(c.ClubId)).Sum(c => c.Goals);
            int oppGoals = m.Clubs.Where(c => !ids.Contains(c.ClubId)).Sum(c => c.Goals);

            if (ourGoals > oppGoals) wins++;
            else if (ourGoals < oppGoals) losses++;
            else draws++;

            counted++;
        }

        return (wins, draws, losses, counted);
    }

    public sealed class FullMatchStatisticsByDayDto
    {
        public DateOnly Date { get; set; }
        public FullMatchStatisticsDto Statistics { get; set; } = new();
    }

    // GET /api/Players/matches/statistics/by-date-range-grouped?playerId=123&clubIds=355651,352016&start=2025-10-01&end=2025-10-31
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
            playerId, clubIds, start, end
        );

        try
        {
            if (playerId <= 0)
                return BadRequest("Informe um playerId válido.");

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

            var startUtc = DateTime.SpecifyKind(start.Date, DateTimeKind.Utc);
            var endExclusiveUtc = DateTime.SpecifyKind(end.Date.AddDays(1), DateTimeKind.Utc);

            // Só partidas em que esse jogador participou por um dos clubes informados
            var q = _db.Matches
                .AsNoTracking()
                .Include(m => m.Clubs).ThenInclude(c => c.Details)
                .Include(m => m.MatchPlayers).ThenInclude(mp => mp.Player)
                .Where(m =>
                    m.MatchPlayers.Any(mp =>
                        mp.Player.PlayerId == playerId &&
                        ids.Contains(mp.ClubId)))
                .Where(m => m.Timestamp >= startUtc && m.Timestamp < endExclusiveUtc);

            var matches = await q.OrderBy(m => m.Timestamp).ToListAsync(ct);
            if (matches.Count == 0)
                return Ok(Array.Empty<PlayerStatisticsByDayDto>());

            // Agrupa por dia (UTC)
            var grouped = matches
                .GroupBy(m => m.Timestamp.Date)
                .Select(g =>
                {
                    var dayMatches = g.ToList();
                    var statsPerMatch = new List<PlayerStatisticsDto>();

                    foreach (var match in dayMatches)
                    {
                        // MatchPlayers desse jogador nesse jogo (e nesses clubes)
                        var playerMatchPlayers = match.MatchPlayers
                            .Where(mp => mp.Player.PlayerId == playerId && ids.Contains(mp.ClubId))
                            .ToList();

                        if (!playerMatchPlayers.Any())
                            continue;

                        // Reuso do aggregator para calcular o PlayerStatisticsDto de UM jogo.
                        // Como estamos passando apenas os MatchPlayers desse jogador, o resultado
                        // será uma linha com MatchesPlayed ~ 1 e agregados do jogo.
                        var statsList = StatsAggregator.BuildPerPlayerMergedByGlobalId(playerMatchPlayers);
                        var stat = statsList.FirstOrDefault();
                        if (stat != null)
                        {
                            // Se quiser, você pode aqui ajustar algum campo (ex.: garantir MatchesPlayed = 1)
                            // stat.MatchesPlayed = 1;

                            statsPerMatch.Add(stat);
                        }
                    }

                    return new PlayerStatisticsByDayDto
                    {
                        Date = DateOnly.FromDateTime(g.Key),
                        Statistics = statsPerMatch
                    };
                })
                .OrderBy(x => x.Date)
                .ToList();

            return Ok(grouped);
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
        [FromQuery] MatchType matchType = MatchType.All,
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

            IQueryable<MatchEntity> q = _db.Matches.AsNoTracking()
                .Where(m => m.Clubs.Any(c => ids.Contains(c.ClubId)));

            if (matchType == MatchType.League)
                q = q.Where(m => m.MatchType == MatchType.League);
            else if (matchType == MatchType.Playoff)
                q = q.Where(m => m.MatchType == MatchType.Playoff);

            if (opponentCount.HasValue)
            {
                var oc = opponentCount.Value;
                q = q.Where(m =>
                    m.MatchPlayers
                     .Where(mp => !ids.Contains(mp.ClubId))
                     .Select(mp => mp.PlayerEntityId)
                     .Distinct()
                     .Count() == oc);
            }

            var totalCount = await q.CountAsync(ct);
            var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
            var skip = (page - 1) * pageSize;

            var matches = await q
                .OrderByDescending(m => m.Timestamp)
                .ThenByDescending(m => m.MatchId)
                .Skip(skip)
                .Take(pageSize)
                .Include(m => m.Clubs).ThenInclude(c => c.Details)
                .Include(m => m.MatchPlayers).ThenInclude(mp => mp.Player)
                .AsNoTracking()
                .ToListAsync(ct);

            var nullDivClubIds = matches
                .SelectMany(m => m.Clubs)
                .Where(c => c.CurrentDivision == null)
                .Select(c => c.ClubId)
                .Distinct()
                .ToList();

            var fallbackDivByClub = nullDivClubIds.Count > 0
                ? await _db.OverallStats
                    .AsNoTracking()
                    .Where(os => nullDivClubIds.Contains(os.ClubId))
                    .Select(os => new { os.ClubId, os.CurrentDivision })
                    .ToDictionaryAsync(x => x.ClubId, x => (int?)x.CurrentDivision, ct)
                : new Dictionary<long, int?>();

            var items = new List<MatchResultDto>(matches.Count);
            foreach (var match in matches)
            {
                var clubs = match.Clubs.OrderBy(c => c.Team).ToList();
                if (clubs.Count != 2) continue;
                var a = clubs[0];
                var b = clubs[1];

                var redA = (short)match.MatchPlayers.Where(p => p.ClubId == a.ClubId).Sum(p => p.Redcards);
                var redB = (short)match.MatchPlayers.Where(p => p.ClubId == b.ClubId).Sum(p => p.Redcards);
                var cntA = match.MatchPlayers.Where(mp => mp.ClubId == a.ClubId).Select(mp => mp.PlayerEntityId).Distinct().Count();
                var cntB = match.MatchPlayers.Where(mp => mp.ClubId == b.ClubId).Select(mp => mp.PlayerEntityId).Distinct().Count();
                var motmId = GetManOfTheMatchId(match);

                var dto = new MatchResultDto
                {
                    MatchId = match.MatchId,
                    Timestamp = match.Timestamp,
                    ClubAName = a.Details?.Name ?? $"Clube {a.ClubId}",
                    ClubAGoals = a.Goals,
                    ClubARedCards = redA,
                    ClubAPlayerCount = cntA,
                    ClubADetails = a.Details == null ? null : ToDetailsDto(a.Details, a.ClubId),
                    ClubASummary = BuildClubSummaryNames(match, a.ClubId, redA, motmId),
                    ClubBName = b.Details?.Name ?? $"Clube {b.ClubId}",
                    ClubBGoals = b.Goals,
                    ClubBRedCards = redB,
                    ClubBPlayerCount = cntB,
                    ClubBDetails = b.Details == null ? null : ToDetailsDto(b.Details, b.ClubId),
                    ClubBSummary = BuildClubSummaryNames(match, b.ClubId, redB, motmId),
                    ResultText = $"{a.Details?.Name ?? "Clube A"} {a.Goals} x {b.Goals} {b.Details?.Name ?? "Clube B"}"
                };
                if (dto.ClubADetails != null) dto.ClubADetails.Team = a.Team.ToString();
                if (dto.ClubBDetails != null) dto.ClubBDetails.Team = b.Team.ToString();
                if (dto.ClubADetails != null)
                    dto.ClubADetails.CurrentDivision = a.CurrentDivision
                        ?? (fallbackDivByClub.TryGetValue(a.ClubId, out var divA) ? divA : null);
                if (dto.ClubBDetails != null)
                    dto.ClubBDetails.CurrentDivision = b.CurrentDivision
                        ?? (fallbackDivByClub.TryGetValue(b.ClubId, out var divB) ? divB : null);
                items.Add(dto);
            }

            return Ok(new PagedResult<MatchResultDto>
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages,
                HasPrevious = page > 1 && totalPages > 0,
                HasNext = page < totalPages,
                Items = items
            });
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
    [FromQuery] MatchType matchType = MatchType.All,
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

            // sane defaults/limits
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

            // base query (sem Include para o Count)
            IQueryable<MatchEntity> q = _db.Matches.AsNoTracking()
                .Where(m => m.Clubs.Any(c => c.ClubId == clubId));

            if (matchType == MatchType.League) q = q.Where(m => m.MatchType == MatchType.League);
            else if (matchType == MatchType.Playoff) q = q.Where(m => m.MatchType == MatchType.Playoff);

            q = ApplyOpponentFilter(q, clubId, opponentCount);

            var totalCount = await q.CountAsync(ct);
            var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

            // Se a página pedida ultrapassar o total, retornamos página vazia mas com metadados corretos
            var skip = (page - 1) * pageSize;

            var matches = await q
                .OrderByDescending(m => m.Timestamp)
                .ThenByDescending(m => m.MatchId)
                .Skip(skip)
                .Take(pageSize)
                .Include(m => m.Clubs).ThenInclude(c => c.Details)
                .Include(m => m.MatchPlayers).ThenInclude(mp => mp.Player)
                .AsNoTracking()
                .ToListAsync(ct);

            var nullDivClubIds = matches
                .SelectMany(m => m.Clubs)
                .Where(c => c.CurrentDivision == null)
                .Select(c => c.ClubId)
                .Distinct()
                .ToList();

            var fallbackDivByClub = nullDivClubIds.Count > 0
                ? await _db.OverallStats
                    .AsNoTracking()
                    .Where(os => nullDivClubIds.Contains(os.ClubId))
                    .Select(os => new { os.ClubId, os.CurrentDivision })
                    .ToDictionaryAsync(x => x.ClubId, x => (int?)x.CurrentDivision, ct)
                : new Dictionary<long, int?>();

            var items = new List<MatchResultDto>(matches.Count);

            foreach (var match in matches)
            {
                var clubs = match.Clubs.OrderBy(c => c.Team).ToList();
                if (clubs.Count != 2) continue;

                var a = clubs[0];
                var b = clubs[1];

                var redA = (short)match.MatchPlayers.Where(p => p.ClubId == a.ClubId).Sum(p => p.Redcards);
                var redB = (short)match.MatchPlayers.Where(p => p.ClubId == b.ClubId).Sum(p => p.Redcards);

                var cntA = match.MatchPlayers.Where(mp => mp.ClubId == a.ClubId).Select(mp => mp.PlayerEntityId).Distinct().Count();
                var cntB = match.MatchPlayers.Where(mp => mp.ClubId == b.ClubId).Select(mp => mp.PlayerEntityId).Distinct().Count();

                var motmId = GetManOfTheMatchId(match);

                var dto = new MatchResultDto
                {
                    MatchId = match.MatchId,
                    Timestamp = match.Timestamp,

                    ClubAName = a.Details?.Name ?? $"Clube {a.ClubId}",
                    ClubAGoals = a.Goals,
                    ClubARedCards = redA,
                    ClubAPlayerCount = cntA,
                    ClubADetails = a.Details == null ? null : ToDetailsDto(a.Details, a.ClubId),
                    ClubASummary = BuildClubSummaryNames(match, a.ClubId, redA, motmId),

                    ClubBName = b.Details?.Name ?? $"Clube {b.ClubId}",
                    ClubBGoals = b.Goals,
                    ClubBRedCards = redB,
                    ClubBPlayerCount = cntB,
                    ClubBDetails = b.Details == null ? null : ToDetailsDto(b.Details, b.ClubId),
                    ClubBSummary = BuildClubSummaryNames(match, b.ClubId, redB, motmId),

                    ResultText = $"{a.Details?.Name ?? "Clube A"} {a.Goals} x {b.Goals} {b.Details?.Name ?? "Clube B"}"
                };

                if (dto.ClubADetails != null) dto.ClubADetails.Team = a.Team.ToString();
                if (dto.ClubBDetails != null) dto.ClubBDetails.Team = b.Team.ToString();

                if (dto.ClubADetails != null)
                    dto.ClubADetails.CurrentDivision = a.CurrentDivision
                        ?? (fallbackDivByClub.TryGetValue(a.ClubId, out var divA) ? divA : null);

                if (dto.ClubBDetails != null)
                    dto.ClubBDetails.CurrentDivision = b.CurrentDivision
                        ?? (fallbackDivByClub.TryGetValue(b.ClubId, out var divB) ? divB : null);

                items.Add(dto);
            }

            var payload = new PagedResult<MatchResultDto>
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages,
                HasPrevious = page > 1 && totalPages > 0,
                HasNext = page < totalPages,
                Items = items
            };

            return Ok(payload);
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

            if (ids.Count == 0)
                return BadRequest("Nenhum clubId válido em 'clubIds'.");

            var matches = await _db.Matches
                .AsNoTracking()
                .Include(m => m.Clubs).ThenInclude(c => c.Details)
                .Include(m => m.MatchPlayers).ThenInclude(mp => mp.Player)
                .Where(m => m.Clubs.Any(c => ids.Contains(c.ClubId)))
                .OrderBy(m => m.Timestamp)
                .ToListAsync(ct);

            var dto = new ClubRecordsDto();

            int winStreak = 0, unbeatenStreak = 0, cleanSheetStreak = 0, scoringStreak = 0;
            int maxWinStreak = 0, maxUnbeatenStreak = 0, maxCleanSheetStreak = 0, maxScoringStreak = 0;

            RecordMatchDto? biggestWin = null;
            RecordMatchDto? biggestLoss = null;
            RecordMatchDto? highestScoring = null;
            int bestWinDiff = 0, bestLossDiff = 0, bestTotal = 0;

            foreach (var match in matches)
            {
                int ourCount = match.Clubs.Count(c => ids.Contains(c.ClubId));
                if (ourCount != 1) continue;

                var ourClub = match.Clubs.First(c => ids.Contains(c.ClubId));
                var oppClub = match.Clubs.FirstOrDefault(c => !ids.Contains(c.ClubId));

                int gf = ourClub.Goals;
                int ga = oppClub?.Goals ?? 0;

                dto.TotalMatches++;
                dto.TotalGoalsFor += gf;
                dto.TotalGoalsAgainst += ga;

                bool isWin = gf > ga;
                bool isDraw = gf == ga;
                bool isLoss = gf < ga;
                bool isCleanSheet = ga == 0;
                bool isScoring = gf > 0;

                if (isWin) dto.TotalWins++;
                else if (isDraw) dto.TotalDraws++;
                else dto.TotalLosses++;

                if (isWin)
                {
                    winStreak++;
                    unbeatenStreak++;
                    int diff = gf - ga;
                    if (diff > bestWinDiff)
                    {
                        bestWinDiff = diff;
                        biggestWin = new RecordMatchDto
                        {
                            MatchId = match.MatchId,
                            Timestamp = match.Timestamp,
                            GoalsFor = gf,
                            GoalsAgainst = ga,
                            OpponentName = oppClub?.Details?.Name
                        };
                    }
                }
                else if (isDraw)
                {
                    winStreak = 0;
                    unbeatenStreak++;
                }
                else
                {
                    winStreak = 0;
                    unbeatenStreak = 0;
                    int diff = ga - gf;
                    if (diff > bestLossDiff)
                    {
                        bestLossDiff = diff;
                        biggestLoss = new RecordMatchDto
                        {
                            MatchId = match.MatchId,
                            Timestamp = match.Timestamp,
                            GoalsFor = gf,
                            GoalsAgainst = ga,
                            OpponentName = oppClub?.Details?.Name
                        };
                    }
                }

                if (isCleanSheet) cleanSheetStreak++;
                else cleanSheetStreak = 0;

                if (isScoring) scoringStreak++;
                else scoringStreak = 0;

                if (winStreak > maxWinStreak) maxWinStreak = winStreak;
                if (unbeatenStreak > maxUnbeatenStreak) maxUnbeatenStreak = unbeatenStreak;
                if (cleanSheetStreak > maxCleanSheetStreak) maxCleanSheetStreak = cleanSheetStreak;
                if (scoringStreak > maxScoringStreak) maxScoringStreak = scoringStreak;

                int total = gf + ga;
                if (total > bestTotal)
                {
                    bestTotal = total;
                    highestScoring = new RecordMatchDto
                    {
                        MatchId = match.MatchId,
                        Timestamp = match.Timestamp,
                        GoalsFor = gf,
                        GoalsAgainst = ga,
                        OpponentName = oppClub?.Details?.Name
                    };
                }
            }

            dto.LongestWinStreak = maxWinStreak;
            dto.LongestUnbeatenStreak = maxUnbeatenStreak;
            dto.LongestCleanSheetStreak = maxCleanSheetStreak;
            dto.LongestScoringStreak = maxScoringStreak;
            dto.CurrentWinStreak = winStreak;
            dto.CurrentUnbeatenStreak = unbeatenStreak;
            dto.BiggestWin = biggestWin;
            dto.BiggestLoss = biggestLoss;
            dto.HighestScoringMatch = highestScoring;

            var allMatchPlayers = matches
                .SelectMany(m => m.MatchPlayers.Where(mp => ids.Contains(mp.ClubId))
                    .Select(mp => new { Match = m, Mp = mp }))
                .ToList();

            static string ResolveName(MatchPlayerEntity mp) =>
                !string.IsNullOrWhiteSpace(mp.ProName) ? mp.ProName : mp.Player?.Playername ?? "";

            var byPlayerMatch = allMatchPlayers
                .GroupBy(x => new { x.Mp.PlayerEntityId, x.Match.MatchId })
                .Select(g => new
                {
                    g.Key.PlayerEntityId,
                    g.Key.MatchId,
                    Timestamp = g.First().Match.Timestamp,
                    Goals = g.Sum(x => (int)x.Mp.Goals),
                    Assists = g.Sum(x => (int)x.Mp.Assists),
                    Saves = g.Sum(x => (int)x.Mp.Saves),
                    Rating = g.Max(x => x.Mp.Rating),
                    Name = g.Select(x => x.Mp).OrderByDescending(mp => mp.MatchId)
                             .Select(mp => ResolveName(mp))
                             .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)) ?? ""
                })
                .ToList();

            var mostGoals = byPlayerMatch.OrderByDescending(x => x.Goals).FirstOrDefault();
            if (mostGoals != null)
                dto.MostGoalsInMatch = new RecordPlayerMatchDto { MatchId = mostGoals.MatchId, Timestamp = mostGoals.Timestamp, PlayerName = mostGoals.Name, Value = mostGoals.Goals };

            var mostAssists = byPlayerMatch.OrderByDescending(x => x.Assists).FirstOrDefault();
            if (mostAssists != null)
                dto.MostAssistsInMatch = new RecordPlayerMatchDto { MatchId = mostAssists.MatchId, Timestamp = mostAssists.Timestamp, PlayerName = mostAssists.Name, Value = mostAssists.Assists };

            var mostSaves = byPlayerMatch.OrderByDescending(x => x.Saves).FirstOrDefault();
            if (mostSaves != null)
                dto.MostSavesInMatch = new RecordPlayerMatchDto { MatchId = mostSaves.MatchId, Timestamp = mostSaves.Timestamp, PlayerName = mostSaves.Name, Value = mostSaves.Saves };

            var highestRating = byPlayerMatch.OrderByDescending(x => x.Rating).FirstOrDefault();
            if (highestRating != null)
                dto.HighestRating = new RecordPlayerMatchDto { MatchId = highestRating.MatchId, Timestamp = highestRating.Timestamp, PlayerName = highestRating.Name, Value = (int)Math.Round(highestRating.Rating) };

            var byPlayerCareer = allMatchPlayers
                .GroupBy(x => x.Mp.PlayerEntityId)
                .Select(g => new
                {
                    PlayerEntityId = g.Key,
                    TotalRedCards = g.Sum(x => (int)x.Mp.Redcards),
                    TotalMoM = g.Count(x => x.Mp.Mom),
                    LastMatchId = g.OrderByDescending(x => x.Match.Timestamp).First().Match.MatchId,
                    LastTimestamp = g.OrderByDescending(x => x.Match.Timestamp).First().Match.Timestamp,
                    Name = g.OrderByDescending(x => x.Match.Timestamp)
                             .Select(x => ResolveName(x.Mp))
                             .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)) ?? ""
                })
                .ToList();

            var mostRedCards = byPlayerCareer.OrderByDescending(x => x.TotalRedCards).FirstOrDefault();
            if (mostRedCards != null)
                dto.MostRedCardsCareer = new RecordPlayerMatchDto { MatchId = mostRedCards.LastMatchId, Timestamp = mostRedCards.LastTimestamp, PlayerName = mostRedCards.Name, Value = mostRedCards.TotalRedCards };

            var mostMoM = byPlayerCareer.OrderByDescending(x => x.TotalMoM).FirstOrDefault();
            if (mostMoM != null)
                dto.MostMoMCareer = new RecordPlayerMatchDto { MatchId = mostMoM.LastMatchId, Timestamp = mostMoM.LastTimestamp, PlayerName = mostMoM.Name, Value = mostMoM.TotalMoM };

            dto.HatTricks = byPlayerMatch
                .Where(x => x.Goals >= 3)
                .OrderByDescending(x => x.Timestamp)
                .Take(50)
                .Select(x => new HatTrickDto
                {
                    MatchId = x.MatchId,
                    Timestamp = x.Timestamp,
                    PlayerName = x.Name,
                    Goals = x.Goals
                })
                .ToList();

            return Ok(dto);
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

            if (ids.Count == 0)
                return BadRequest("Nenhum clubId válido em 'clubIds'.");

            var matches = await _db.Matches
                .AsNoTracking()
                .Include(m => m.Clubs).ThenInclude(c => c.Details)
                .Where(m => m.Clubs.Any(c => ids.Contains(c.ClubId)))
                .OrderBy(m => m.Timestamp)
                .ToListAsync(ct);

            var opponentMap = new Dictionary<long, OpponentRecordDto>();
            int totalProcessed = 0;

            foreach (var match in matches)
            {
                int ourCount = match.Clubs.Count(c => ids.Contains(c.ClubId));
                if (ourCount != 1) continue;

                var ourClub = match.Clubs.First(c => ids.Contains(c.ClubId));
                var oppClub = match.Clubs.FirstOrDefault(c => !ids.Contains(c.ClubId));
                if (oppClub == null) continue;

                totalProcessed++;

                int gf = ourClub.Goals;
                int ga = oppClub.Goals;

                if (!opponentMap.TryGetValue(oppClub.ClubId, out var rec))
                {
                    rec = new OpponentRecordDto
                    {
                        ClubId = oppClub.ClubId,
                        Name = oppClub.Details?.Name ?? $"Clube {oppClub.ClubId}"
                    };
                    opponentMap[oppClub.ClubId] = rec;
                }

                rec.Matches++;
                rec.GoalsFor += gf;
                rec.GoalsAgainst += ga;
                rec.GoalDiff = rec.GoalsFor - rec.GoalsAgainst;
                rec.LastMatch = match.Timestamp;

                if (gf > ga)
                {
                    rec.Wins++;
                    int diff = gf - ga;
                    if (rec.BiggestWinMatchId == null || diff > (rec.BiggestWinGF - rec.BiggestWinGA))
                    {
                        rec.BiggestWinMatchId = match.MatchId;
                        rec.BiggestWinGF = gf;
                        rec.BiggestWinGA = ga;
                    }
                }
                else if (gf < ga)
                {
                    rec.Losses++;
                    int diff = ga - gf;
                    if (rec.BiggestLossMatchId == null || diff > (rec.BiggestLossGA - rec.BiggestLossGF))
                    {
                        rec.BiggestLossMatchId = match.MatchId;
                        rec.BiggestLossGF = gf;
                        rec.BiggestLossGA = ga;
                    }
                }
                else
                {
                    rec.Draws++;
                }
            }

            foreach (var rec in opponentMap.Values)
                rec.WinRate = rec.Matches > 0 ? Math.Round(rec.Wins * 100.0 / rec.Matches, 1) : 0;

            var opponents = opponentMap.Values.OrderByDescending(o => o.Matches).ToList();

            return Ok(new OpponentsAnalysisDto
            {
                TotalMatches = totalProcessed,
                TotalOpponents = opponents.Count,
                Opponents = opponents
            });
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
            var exists = await _db.MatchClubs.AnyAsync(c => c.ClubId == clubId, ct);
            if (!exists) return NotFound(new { message = "Clube não encontrado" });

            var matchIds = await _db.MatchClubs
                .Where(mc => mc.ClubId == clubId)
                .Select(mc => mc.MatchId)
                .Distinct()
                .ToListAsync(ct);

            if (matchIds.Count == 0) return NoContent();

            var strategy = _db.Database.CreateExecutionStrategy();

            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _db.Database.BeginTransactionAsync(ct);
                try
                {
                    var statsIds = await _db.MatchPlayers
                        .Where(mp => matchIds.Contains(mp.MatchId))
                        .Select(mp => mp.PlayerMatchStatsEntityId)
                        .Where(id => id != null)
                        .Cast<long>()
                        .Distinct()
                        .ToListAsync(ct);

                    await _db.PlayerMatchStats.Where(pms => statsIds.Contains(pms.Id)).ExecuteDeleteAsync(ct);
                    await _db.MatchPlayers.Where(mp => matchIds.Contains(mp.MatchId)).ExecuteDeleteAsync(ct);
                    await _db.MatchClubs.Where(mc => matchIds.Contains(mc.MatchId)).ExecuteDeleteAsync(ct);
                    await _db.Matches.Where(m => matchIds.Contains(m.MatchId)).ExecuteDeleteAsync(ct);

                    await tx.CommitAsync(ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during transaction in DeleteMatchesByClub for clubId={ClubId}", clubId);
                    await tx.RollbackAsync(ct);
                    throw;
                }
            });

            return NoContent();
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

            if (ids.Count == 0)
                return BadRequest("invalid clubIds");

            var qBase =
                from mc in _db.MatchClubs.AsNoTracking()
                where ids.Contains(mc.ClubId)
                from mcOpp in _db.MatchClubs.AsNoTracking()
                    .Where(x => x.MatchId == mc.MatchId && x.ClubId != mc.ClubId)
                select new
                {
                    mc.MatchId,
                    mc.Date,
                    OpponentPlayers =
                        _db.MatchPlayers.AsNoTracking()
                            .Where(mp => mp.MatchId == mcOpp.MatchId && mp.ClubId == mcOpp.ClubId)
                            .Select(mp => mp.PlayerEntityId)
                            .Distinct()
                            .Count()
                };

            if (opponentCount.HasValue)
                qBase = qBase.Where(x => x.OpponentPlayers == opponentCount.Value);

            var lastMatches = await qBase
                .GroupBy(x => x.MatchId)
                .Select(g => new { MatchId = g.Key, Date = g.Max(x => x.Date) })
                .OrderByDescending(x => x.Date)
                .Take(count)
                .ToListAsync(ct);

            if (lastMatches.Count == 0)
                return Ok(new { players = Array.Empty<object>(), clubs = Array.Empty<object>() });

            var lastMatchIds = lastMatches.Select(x => x.MatchId).ToList();

            var players = await _db.MatchPlayers
                .AsNoTracking()
                .Where(p => lastMatchIds.Contains(p.MatchId) && ids.Contains(p.ClubId))
                .Include(p => p.Player)
                .Include(p => p.Match)
                .ToListAsync(ct);

            var playerStats = StatsAggregator.BuildPerPlayerMergedByGlobalId(players);
            var clubStatsSingle = StatsAggregator.BuildSingleClubFromPlayers(players, clubName: "Clubes agrupados");

            return Ok(new
            {
                players = playerStats,
                clubs = new[] { clubStatsSingle }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetGroupedLimited for clubIds={ClubIds}", clubIds);
            return StatusCode(500, "Erro interno ao buscar estatísticas agrupadas.");
        }
    }

    private IQueryable<MatchEntity> BaseClubMatchesQuery(long clubId) =>
    _db.Matches
       .AsNoTracking()
       .Include(m => m.Clubs.Where(c => c.ClubId == clubId)).ThenInclude(c => c.Details)
       .Include(m => m.MatchPlayers).ThenInclude(mp => mp.Player)
       .Where(m => m.Clubs.Any(c => c.ClubId == clubId));

    private static IQueryable<MatchEntity> ApplyOpponentFilter(IQueryable<MatchEntity> q, long clubId, int? opponentCount)
    {
        if (!opponentCount.HasValue) return q;
        var oc = opponentCount.Value;
        return q.Where(m =>
            m.MatchPlayers
             .Where(mp => mp.Player.ClubId != clubId)
             .Select(mp => mp.PlayerEntityId)
             .Distinct()
             .Count() == oc);
    }

    private static ClubDetailsDto ToDetailsDto(ClubDetailsEntity d, long clubId) =>
        new()
        {
            Name = d.Name ?? $"Clube {clubId}",
            ClubId = clubId,
            RegionId = d.RegionId,
            TeamId = d.TeamId,
            StadName = d.StadName,
            KitId = d.KitId,
            CustomKitId = d.CustomKitId,
            CustomAwayKitId = d.CustomAwayKitId,
            CustomThirdKitId = d.CustomThirdKitId,
            CustomKeeperKitId = d.CustomKeeperKitId,
            KitColor1 = d.KitColor1,
            KitColor2 = d.KitColor2,
            KitColor3 = d.KitColor3,
            KitColor4 = d.KitColor4,
            KitAColor1 = d.KitAColor1,
            KitAColor2 = d.KitAColor2,
            KitAColor3 = d.KitAColor3,
            KitAColor4 = d.KitAColor4,
            KitThrdColor1 = d.KitThrdColor1,
            KitThrdColor2 = d.KitThrdColor2,
            KitThrdColor3 = d.KitThrdColor3,
            KitThrdColor4 = d.KitThrdColor4,
            DCustomKit = d.DCustomKit,
            CrestColor = d.CrestColor,
            CrestAssetId = d.CrestAssetId,
            SelectedKitType = d.SelectedKitType,
        };

    private static long? GetManOfTheMatchId(MatchEntity match)
    {
        var flagged = match.MatchPlayers
            .Where(mp => mp.Mom)
            .Select(mp => new { mp.PlayerEntityId, mp.Rating, Score = mp.Goals + mp.Assists })
            .ToList();

        if (flagged.Count == 1) return flagged[0].PlayerEntityId;
        if (flagged.Count > 1)
            return flagged
                .OrderByDescending(x => x.Rating)
                .ThenByDescending(x => x.Score)
                .ThenBy(x => x.PlayerEntityId)
                .First().PlayerEntityId;

        var best = match.MatchPlayers
            .Select(mp => new { mp.PlayerEntityId, mp.Rating, Score = mp.Goals + mp.Assists })
            .OrderByDescending(x => x.Rating)
            .ThenByDescending(x => x.Score)
            .ThenBy(x => x.PlayerEntityId)
            .FirstOrDefault();

        return best?.PlayerEntityId;
    }

    private static List<long> ParseClubIdsFromConfig(IConfiguration config)
    {
        var raw = config["EAFCBackgroundWorkerSettings:ClubIds"] ?? config["EAFCBackgroundWorkerSettings:ClubId"];
        if (string.IsNullOrWhiteSpace(raw)) return new List<long>();
        var parts = raw.Split(new[] { ';', ',', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        return parts
            .Select(p => long.TryParse(p.Trim(), out var v) ? (long?)v : null)
            .Where(v => v.HasValue && v.Value > 0)
            .Select(v => v!.Value)
            .Distinct()
            .ToList();
    }

    private static int ClampOpp(int value) => Math.Min(MaxOpponentPlayers, Math.Max(MinOpponentPlayers, value));

    private static int? ReadOppAliasOrNull(HttpRequest req, int? opponentCount)
    {
        if (opponentCount.HasValue) return opponentCount;
        return req.Query.TryGetValue("opp", out var v) && int.TryParse(v, out var parsed) ? parsed : null;
    }

    private static ClubMatchSummaryDto BuildClubSummaryNames(MatchEntity match, long cid, short redCards, long? motmId)
    {
        static bool IsGk(string? pos)
        {
            if (string.IsNullOrWhiteSpace(pos)) return false;
            var p = pos.Trim().ToUpperInvariant();
            return p is "GK" or "GOL" or "GOALKEEPER" or "GOLEIRO";
        }

        var clubPlayers = match.MatchPlayers.Where(mp => mp.ClubId == cid).ToList();

        var hatTrickNames = clubPlayers
            .GroupBy(mp => mp.PlayerEntityId)
            .Select(g => new
            {
                Goals = g.Sum(x => (int)x.Goals),
                Name = g.Select(x => x.Player?.Playername).FirstOrDefault()
            })
            .Where(x => x.Goals >= 3)
            .Select(x => x.Name)
            .ToList();

        var gkName = clubPlayers
            .Where(mp => IsGk(mp.Pos))
            .GroupBy(mp => mp.PlayerEntityId)
            .Select(g => new
            {
                CleanSheetsGk = g.Sum(x => (int)x.Cleansheetsgk),
                Saves = g.Sum(x => (int)x.Saves),
                Rating = g.Max(x => x.Rating),
                Name = g.Select(x => x.Player?.Playername).FirstOrDefault()
            })
            .OrderByDescending(x => x.CleanSheetsGk)
            .ThenByDescending(x => x.Saves)
            .ThenByDescending(x => x.Rating)
            .ThenBy(x => x.Name)
            .Select(x => x.Name)
            .FirstOrDefault();

        string? motmName = null;
        if (motmId.HasValue && clubPlayers.Any(mp => mp.PlayerEntityId == motmId.Value))
            motmName = clubPlayers.Where(mp => mp.PlayerEntityId == motmId.Value).Select(mp => mp.Player?.Playername).FirstOrDefault();

        var dnfWinner = match.Clubs.FirstOrDefault(c => c.WinnerByDnf);
        var disconnected = dnfWinner != null && dnfWinner.ClubId != cid;

        return new ClubMatchSummaryDto
        {
            RedCards = redCards,
            HadHatTrick = hatTrickNames.Count > 0,
            HatTrickPlayerNames = hatTrickNames,
            GoalkeeperPlayerName = gkName,
            ManOfTheMatchPlayerName = motmName,
            Disconnected = disconnected
        };
    }

    // GET /api/clubs/{clubId}/goals/analysis?from=2025-01-01&to=2025-12-31
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

            // Matches for this club in the range
            var matchesInRange = await _db.MatchClubs
                .AsNoTracking()
                .Where(mc => mc.ClubId == clubId
                          && mc.Match.Timestamp >= fromUtc
                          && mc.Match.Timestamp <= toUtc)
                .Select(mc => new { mc.MatchId, mc.Match.Timestamp, mc.Goals })
                .ToListAsync(ct);

            var matchIdList = matchesInRange.Select(m => m.MatchId).Distinct().ToList();
            var totalGoals = matchesInRange.Sum(m => (int)m.Goals);
            var timestampByMatch = matchesInRange
                .GroupBy(m => m.MatchId)
                .ToDictionary(g => g.Key, g => g.First().Timestamp);

            // Goal links — used only for pairs, trios and pass flow
            var goalLinks = await _db.MatchGoalLinks
                .AsNoTracking()
                .Where(g => matchIdList.Contains(g.MatchId) && g.ClubId == clubId)
                .ToListAsync(ct);

            // MatchPlayers with Player loaded — source of truth for goals/assists/preassists
            // (identical logic to BuildPerPlayerMergedByGlobalId used by the stats page)
            var allMatchPlayers = await _db.MatchPlayers
                .AsNoTracking()
                .Include(mp => mp.Player)
                .Where(mp => matchIdList.Contains(mp.MatchId) && mp.ClubId == clubId)
                .ToListAsync(ct);

            // Name map keyed by PlayerEntityId (used to resolve goal link names)
            var nameMap = allMatchPlayers
                .Where(mp => mp.Player != null)
                .GroupBy(mp => mp.PlayerEntityId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(mp => mp.MatchId)
                          .Select(mp => !string.IsNullOrWhiteSpace(mp.ProName) ? mp.ProName : mp.Player?.Playername)
                          .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)) ?? "");

            // Fallback names for goal links that reference players not in our MatchPlayers
            var involvedIds = goalLinks
                .SelectMany(g => new[] { (long?)g.ScorerPlayerEntityId, g.AssistPlayerEntityId, g.PreAssistPlayerEntityId })
                .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();

            var missingIds = involvedIds.Where(id => !nameMap.ContainsKey(id) || string.IsNullOrWhiteSpace(nameMap[id])).ToList();
            if (missingIds.Count > 0)
            {
                var fallbacks = await _db.Players
                    .AsNoTracking()
                    .Where(p => missingIds.Contains(p.Id))
                    .Select(p => new { p.Id, p.Playername })
                    .ToListAsync(ct);
                foreach (var f in fallbacks)
                    if (!string.IsNullOrWhiteSpace(f.Playername))
                        nameMap[f.Id] = f.Playername;
            }

            string Resolve(long? id) =>
                id.HasValue && nameMap.TryGetValue(id.Value, out var n) && !string.IsNullOrWhiteSpace(n) ? n : null!;

            // Build link DTOs (pairs, trios, pass flow)
            var linkDtos = goalLinks
                .Select(g => new GoalAnalysisLinkDto
                {
                    MatchId = g.MatchId,
                    MatchTimestamp = timestampByMatch.TryGetValue(g.MatchId, out var ts) ? ts : DateTime.MinValue,
                    ScorerName = Resolve(g.ScorerPlayerEntityId) ?? "Desconhecido",
                    AssistName = g.AssistPlayerEntityId.HasValue ? Resolve(g.AssistPlayerEntityId) : null,
                    PreAssistName = g.PreAssistPlayerEntityId.HasValue ? Resolve(g.PreAssistPlayerEntityId) : null,
                })
                .OrderByDescending(l => l.MatchTimestamp)
                .ToList();

            // Player participation ranking — group by global PlayerId (same as BuildPerPlayerMergedByGlobalId)
            var playerMap = allMatchPlayers
                .Where(mp => mp.Player != null)
                .GroupBy(mp => mp.Player.PlayerId)
                .Select(g =>
                {
                    var repr = g.OrderByDescending(mp => mp.MatchId).First();
                    var name = !string.IsNullOrWhiteSpace(repr.ProName)
                        ? repr.ProName
                        : repr.Player?.Playername ?? "Desconhecido";
                    var goals    = g.Sum(mp => (int)mp.Goals);
                    var assists  = g.Sum(mp => (int)mp.Assists);
                    var pre      = g.Sum(mp => (int)mp.PreAssists);
                    return new GoalAnalysisPlayerDto
                    {
                        Name       = name,
                        Goals      = goals,
                        Assists    = assists,
                        PreAssists = pre,
                        Total      = goals + assists + pre,
                    };
                })
                .Where(p => p.Total > 0)
                .ToDictionary(p => p.Name);

            var pairs = linkDtos
                .Where(l => !string.IsNullOrEmpty(l.AssistName))
                .GroupBy(l => (l.AssistName!, l.ScorerName))
                .Select(g => new GoalAnalysisPairDto { From = g.Key.Item1, To = g.Key.ScorerName, Count = g.Count() })
                .OrderByDescending(p => p.Count)
                .ToList();

            var trios = linkDtos
                .Where(l => !string.IsNullOrEmpty(l.PreAssistName) && !string.IsNullOrEmpty(l.AssistName))
                .GroupBy(l => (l.PreAssistName!, l.AssistName!, l.ScorerName))
                .Select(g => new GoalAnalysisTrioDto { Pre = g.Key.Item1, Assist = g.Key.Item2, Scorer = g.Key.ScorerName, Count = g.Count() })
                .OrderByDescending(t => t.Count)
                .ToList();

            return Ok(new GoalAnalysisResponseDto
            {
                ClubId = clubId,
                From = fromUtc,
                To = toUtc,
                TotalMatches = matchIdList.Count,
                TotalGoals = totalGoals,
                LinkedGoals = goalLinks.Count,
                TotalAssists = goalLinks.Count(g => g.AssistPlayerEntityId.HasValue),
                TotalPreAssists = goalLinks.Count(g => g.PreAssistPlayerEntityId.HasValue),
                Players = playerMap.Values.OrderByDescending(p => p.Total).ThenByDescending(p => p.Goals).ToList(),
                Pairs = pairs,
                Trios = trios,
                GoalLinks = linkDtos,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetGoalAnalysis clubId={ClubId}", clubId);
            return StatusCode(500, "Erro interno ao buscar análise de gols.");
        }
    }
}
