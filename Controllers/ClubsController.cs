﻿using EAFCMatchTracker.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace EAFCMatchTracker.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ClubsController : ControllerBase
{
    private const int MinOpponentPlayers = 2;
    private const int MaxOpponentPlayers = 11;

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
        var ids = ParseClubIdsFromConfig(_config);
        if (ids.Count == 0) return Ok(Array.Empty<ClubListItemDto>());

        var flat = await _db.MatchClubs
            .AsNoTracking()
            .Where(mc => ids.Contains(mc.ClubId))
            .Select(mc => new
            {
                mc.ClubId,
                mc.Details.Name,
                Crest = mc.Team.ToString(),
                Ts = mc.Match.Timestamp
            })
            .OrderByDescending(x => x.Ts)
            .ToListAsync();

        var clubs = flat
            .GroupBy(x => x.ClubId)
            .Select(g =>
            {
                var name = g.Select(x => x.Name).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)) ?? $"Clube {g.Key}";
                var crest = g.Select(x => x.Crest).FirstOrDefault(c => !string.IsNullOrWhiteSpace(c));
                return new ClubListItemDto { ClubId = g.Key, Name = name, CrestAssetId = crest };
            })
            .OrderBy(x => x.Name)
            .ToList();

        return Ok(clubs);
    }

    [HttpGet("{clubId:long}/players/attributes")]
    public async Task<ActionResult<List<PlayerAttributeSnapshotDto>>> GetClubPlayersAttributes(
    long clubId,
    [FromQuery] int count = 10,
    CancellationToken ct = default)
    {
        if (clubId <= 0) return BadRequest("Informe um clubId válido.");
        if (count <= 0) return BadRequest("O número de partidas deve ser maior que zero.");

        // Últimas N partidas do clube (com jogadores e stats carregados)
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

        // Para cada jogador do clube, pegamos a ocorrência MAIS NOVA (pela timestamp do match)
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
            .Where(x => x.Player != null) // sanity
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

    [HttpGet("{clubId:long}/players/aggregate")]
    public async Task<ActionResult<List<PlayerStatisticsDto>>> GetClubPlayersAggregate(
    long clubId,
    [FromQuery] int count = 10,
    [FromQuery] int? opponentCount = null,
    CancellationToken ct = default)
    {
        if (clubId <= 0) return BadRequest("Informe um clubId válido.");
        if (count <= 0) return BadRequest("O número de partidas deve ser maior que zero.");

        // Reutiliza sua própria lógica já existente:
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

    [HttpGet("{clubId:long}/overall-and-playoffs")]
    public async Task<IActionResult> GetClubOverallAndPlayoffs(long clubId, CancellationToken ct)
    {
        if (clubId <= 0)
            return BadRequest("Informe um clubId válido.");

        var overallEntities = await _db.OverallStats
            .AsNoTracking()
            .Where(o => o.ClubId == clubId)
            .ToListAsync(ct);

        var playoffEntities = await _db.PlayoffAchievements
            .AsNoTracking()
            .Where(p => p.ClubId == clubId)
            .ToListAsync(ct);

        var clubsOverall = StatsAggregator.BuildClubsOverall(overallEntities);
        var clubsPlayoffAchievements = StatsAggregator.BuildClubsPlayoffAchievements(playoffEntities);

        // Mantém o payload compatível com o já utilizado no front:
        // { ClubsOverall: [...], ClubsPlayoffAchievements: [...] }
        return Ok(new
        {
            ClubsOverall = clubsOverall,
            ClubsPlayoffAchievements = clubsPlayoffAchievements
        });
    }

    [HttpGet("{clubId:long}/matches/statistics")]
    public async Task<ActionResult<FullMatchStatisticsDto>> GetMatchStatistics(long clubId, CancellationToken ct)
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

    [HttpGet("{clubId:long}/matches/statistics/limited")]
    public async Task<IActionResult> GetMatchStatisticsLimited(long clubId, [FromQuery] int? opponentCount, [FromQuery] int count = 10, CancellationToken ct = default)
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

    [HttpGet("{clubId:long}/matches/results")]
    public async Task<ActionResult<List<MatchResultDto>>> GetMatchResults(
        long clubId,
        [FromQuery] MatchType matchType = MatchType.All,
        [FromQuery] int? opponentCount = null,
        CancellationToken ct = default)
    {
        if (clubId <= 0) return BadRequest("Informe um clubId válido.");

        opponentCount = ReadOppAliasOrNull(Request, opponentCount);
        if (opponentCount.HasValue)
        {
            opponentCount = ClampOpp(opponentCount.Value);
            if (opponentCount < MinOpponentPlayers || opponentCount > MaxOpponentPlayers)
                return BadRequest($"opponentCount deve estar entre {MinOpponentPlayers} e {MaxOpponentPlayers}.");
        }

        var q = _db.Matches.AsNoTracking().Where(m => m.Clubs.Any(c => c.ClubId == clubId));
        if (matchType == MatchType.League) q = q.Where(m => m.MatchType == MatchType.League);
        else if (matchType == MatchType.Playoff) q = q.Where(m => m.MatchType == MatchType.Playoff);
        q = ApplyOpponentFilter(q, clubId, opponentCount);

        var matches = await q
            .Include(m => m.Clubs).ThenInclude(c => c.Details)
            .Include(m => m.MatchPlayers).ThenInclude(mp => mp.Player)
            .AsNoTracking()
            .OrderByDescending(m => m.Timestamp)
            .ToListAsync(ct);

        // === Prefetch das divisões atuais de TODOS os clubes envolvidos nessa página ===
        var allClubIds = matches
            .SelectMany(m => m.Clubs.Select(c => c.ClubId))
            .Distinct()
            .ToList();

        var divByClub = await _db.OverallStats
            .AsNoTracking()
            .Where(os => allClubIds.Contains(os.ClubId))
            .Select(os => new { os.ClubId, os.CurrentDivision })
            .ToDictionaryAsync(x => x.ClubId, x => (int?)x.CurrentDivision, ct);

        // ==============================================================================

        var result = new List<MatchResultDto>(matches.Count);

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

            // Preenche Team (já fazia)
            if (dto.ClubADetails != null) dto.ClubADetails.Team = a.Team.ToString();
            if (dto.ClubBDetails != null) dto.ClubBDetails.Team = b.Team.ToString();

            // === Preenche CurrentDivision a partir do dicionário ===
            if (dto.ClubADetails != null && divByClub.TryGetValue(a.ClubId, out var divA))
                dto.ClubADetails.CurrentDivision = divA;

            if (dto.ClubBDetails != null && divByClub.TryGetValue(b.ClubId, out var divB))
                dto.ClubBDetails.CurrentDivision = divB;

            result.Add(dto);
        }

        return Ok(result);
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


    [HttpDelete("{clubId:long}/matches")]
    public async Task<IActionResult> DeleteMatchesByClub(long clubId, CancellationToken ct)
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
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        });

        return NoContent();
    }

    [HttpGet("grouped/matches/statistics/limited")]
    public async Task<IActionResult> GetGroupedLimited(
        [FromQuery] string clubIds,
        [FromQuery] int count = 20,
        [FromQuery] int? opponentCount = null,
        CancellationToken ct = default)
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

        // Base: MatchClubs dos clubes selecionados + descobrir nº de jogadores do oponente por jogo
        var qBase =
            from mc in _db.MatchClubs.AsNoTracking()
            where ids.Contains(mc.ClubId)

            // "join" para o oponente usando from ... Where (evita Equals de anônimo)
            from mcOpp in _db.MatchClubs.AsNoTracking()
                .Where(x => x.MatchId == mc.MatchId && x.ClubId != mc.ClubId)

                // Conta jogadores do oponente via subconsulta correlacionada
            select new
            {
                mc.MatchId,
                mc.Date,
                OpponentPlayers =
                    _db.MatchPlayers.AsNoTracking()
                        .Where(mp => mp.MatchId == mcOpp.MatchId && mp.ClubId == mcOpp.ClubId)
                        .Select(mp => mp.PlayerEntityId)  // opcional: Distinct() se houver risco de duplicatas
                        .Distinct()
                        .Count()
            };

        if (opponentCount.HasValue)
            qBase = qBase.Where(x => x.OpponentPlayers == opponentCount.Value);

        // Pegar os N jogos globais (no conjunto)
        var lastMatches = await qBase
            .GroupBy(x => x.MatchId)
            .Select(g => new { MatchId = g.Key, Date = g.Max(x => x.Date) })
            .OrderByDescending(x => x.Date)
            .Take(count)
            .ToListAsync(ct);

        if (lastMatches.Count == 0)
            return Ok(new { players = Array.Empty<object>(), clubs = Array.Empty<object>() });

        var lastMatchIds = lastMatches.Select(x => x.MatchId).ToList();

        // Players somente dos clubes selecionados nesses jogos
        var players = await _db.MatchPlayers
            .AsNoTracking()
            .Where(p => lastMatchIds.Contains(p.MatchId) && ids.Contains(p.ClubId))
            .Include(p => p.Player) // precisamos do Player.PlayerId e Player.Playername
            .ToListAsync(ct);

        // === Aggregation no modo "clubes como um só" ===
        var playerStats = StatsAggregator.BuildPerPlayerMergedByGlobalId(players);
        var clubStatsSingle = StatsAggregator.BuildSingleClubFromPlayers(players, clubName: "Clubes agrupados");

        return Ok(new
        {
            players = playerStats,
            clubs = new[] { clubStatsSingle }
        });
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
}
