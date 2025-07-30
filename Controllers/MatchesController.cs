using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/[controller]")]
public class MatchesController : ControllerBase
{
    private readonly EAFCContext _dbContext;

    public MatchesController(EAFCContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllMatches()
    {
        var matches = await _dbContext.Matches
            .Include(m => m.Clubs)
                .ThenInclude(c => c.Details)
            .Include(m => m.MatchPlayers)
                .ThenInclude(mp => mp.Player)
            .OrderByDescending(m => m.Timestamp)
            .ToListAsync();

        var matchDtos = matches.Select(m => new MatchDto
        {
            MatchId = m.MatchId,
            Timestamp = m.Timestamp,
            MatchType = m.MatchType,
            Clubs = m.Clubs.Select(c => new MatchClubDto
            {
                ClubId = c.ClubId,
                Date = c.Date,
                GameNumber = c.GameNumber,
                Goals = c.Goals,
                GoalsAgainst = c.GoalsAgainst,
                Losses = c.Losses,
                MatchType = c.MatchType,
                Result = c.Result,
                Score = c.Score,
                SeasonId = c.SeasonId,
                Team = c.Team,
                Ties = c.Ties,
                Wins = c.Wins,
                WinnerByDnf = c.WinnerByDnf,
                Details = c.Details == null ? null : new ClubDetailsDto
                {
                    Name = c.Details.Name,
                    RegionId = c.Details.RegionId,
                    TeamId = c.Details.TeamId,
                    StadName = c.Details.StadName,
                    KitId = c.Details.KitId,
                    CustomKitId = c.Details.CustomKitId,
                    CustomAwayKitId = c.Details.CustomAwayKitId,
                    CustomThirdKitId = c.Details.CustomThirdKitId,
                    CustomKeeperKitId = c.Details.CustomKeeperKitId,
                    KitColor1 = c.Details.KitColor1,
                    KitColor2 = c.Details.KitColor2,
                    KitColor3 = c.Details.KitColor3,
                    KitColor4 = c.Details.KitColor4,
                    KitAColor1 = c.Details.KitAColor1,
                    KitAColor2 = c.Details.KitAColor2,
                    KitAColor3 = c.Details.KitAColor3,
                    KitAColor4 = c.Details.KitAColor4,
                    KitThrdColor1 = c.Details.KitThrdColor1,
                    KitThrdColor2 = c.Details.KitThrdColor2,
                    KitThrdColor3 = c.Details.KitThrdColor3,
                    KitThrdColor4 = c.Details.KitThrdColor4,
                    DCustomKit = c.Details.DCustomKit,
                    CrestColor = c.Details.CrestColor,
                    CrestAssetId = c.Details.CrestAssetId
                }
            }).ToList(),
            Players = m.MatchPlayers.Select(p => new MatchPlayerDto
            {
                PlayerId = p.Player.PlayerId,
                ClubId = p.ClubId,
                Playername = p.Player.Playername,
                Goals = p.Goals,
                Assists = p.Assists,
                Rating = p.Rating,
                Cleansheetsany = p.Cleansheetsany,
                Cleansheetsdef = p.Cleansheetsdef,
                Cleansheetsgk = p.Cleansheetsgk,
                Losses = p.Losses,
                Mom = p.Mom,
                Passattempts = p.Passattempts,
                Passesmade = p.Passesmade,
                Realtimegame = p.Realtimegame,
                Realtimeidle = p.Realtimeidle,
                Redcards = p.Redcards,
                Saves = p.Saves,
                Score = p.Score,
                Shots = p.Shots,
                Tackleattempts = p.Tackleattempts,
                Tacklesmade = p.Tacklesmade,
                Vproattr = p.Vproattr,
                Vprohackreason = p.Vprohackreason,
                Wins = p.Wins,
                Pos = p.Pos,
                Namespace = p.Namespace,
                Stats = new PlayerMatchStatsDto
                {
                    Aceleracao = p.Player.PlayerMatchStats.Aceleracao,
                    Pique = p.Player.PlayerMatchStats.Pique,
                    Finalizacao = p.Player.PlayerMatchStats.Finalizacao,
                    Falta = p.Player.PlayerMatchStats.Falta,
                    Cabeceio = p.Player.PlayerMatchStats.Cabeceio,
                    ForcaDoChute = p.Player.PlayerMatchStats.ForcaDoChute,
                    ChuteLonge = p.Player.PlayerMatchStats.ChuteLonge,
                    Voleio = p.Player.PlayerMatchStats.Voleio,
                    Penalti = p.Player.PlayerMatchStats.Penalti,
                    Visao = p.Player.PlayerMatchStats.Visao,
                    Cruzamento = p.Player.PlayerMatchStats.Cruzamento,
                    Lancamento = p.Player.PlayerMatchStats.Lancamento,
                    PasseCurto = p.Player.PlayerMatchStats.PasseCurto,
                    Curva = p.Player.PlayerMatchStats.Curva,
                    Agilidade = p.Player.PlayerMatchStats.Agilidade,
                    Equilibrio = p.Player.PlayerMatchStats.Equilibrio,
                    PosAtaqueInutil = p.Player.PlayerMatchStats.PosAtaqueInutil,
                    ControleBola = p.Player.PlayerMatchStats.ControleBola,
                    Conducao = p.Player.PlayerMatchStats.Conducao,
                    Interceptacaos = p.Player.PlayerMatchStats.Interceptacaos,
                    NocaoDefensiva = p.Player.PlayerMatchStats.NocaoDefensiva,
                    DivididaEmPe = p.Player.PlayerMatchStats.DivididaEmPe,
                    Carrinho = p.Player.PlayerMatchStats.Carrinho,
                    Impulsao = p.Player.PlayerMatchStats.Impulsao,
                    Folego = p.Player.PlayerMatchStats.Folego,
                    Forca = p.Player.PlayerMatchStats.Forca,
                    Reacao = p.Player.PlayerMatchStats.Reacao,
                    Combatividade = p.Player.PlayerMatchStats.Combatividade,
                    Frieza = p.Player.PlayerMatchStats.Frieza,
                    ElasticidadeGL = p.Player.PlayerMatchStats.ElasticidadeGL,
                    ManejoGL = p.Player.PlayerMatchStats.ManejoGL,
                    ChuteGL = p.Player.PlayerMatchStats.ChuteGL,
                    ReflexosGL = p.Player.PlayerMatchStats.ReflexosGL,
                    PosGL = p.Player.PlayerMatchStats.PosGL
                }
            }).ToList()
        }).ToList();

        return Ok(matchDtos);
    }

    [HttpGet("{matchId:long}")]
    public async Task<IActionResult> GetMatchById(long matchId)
    {
        var match = await _dbContext.Matches
            .Include(m => m.Clubs)
                .ThenInclude(c => c.Details)
            .Include(m => m.MatchPlayers)
                .ThenInclude(mp => mp.Player)
            .FirstOrDefaultAsync(m => m.MatchId == matchId);

        if (match == null)
            return NotFound();

        var matchDtos = new MatchDto
        {
            MatchId = match.MatchId,
            Timestamp = match.Timestamp,
            MatchType = match.MatchType,  // Incluído MatchType
            Clubs = match.Clubs.Select(c => new MatchClubDto
            {
                ClubId = c.ClubId,
                Date = c.Date,
                GameNumber = c.GameNumber,
                Goals = c.Goals,
                GoalsAgainst = c.GoalsAgainst,
                Losses = c.Losses,
                MatchType = c.MatchType,
                Result = c.Result,
                Score = c.Score,
                SeasonId = c.SeasonId,
                Team = c.Team,
                Ties = c.Ties,
                Wins = c.Wins,
                WinnerByDnf = c.WinnerByDnf,
                Details = c.Details == null ? null : new ClubDetailsDto
                {
                    Name = c.Details.Name,
                    RegionId = c.Details.RegionId,
                    TeamId = c.Details.TeamId,
                    StadName = c.Details.StadName,
                    KitId = c.Details.KitId,
                    CustomKitId = c.Details.CustomKitId,
                    CustomAwayKitId = c.Details.CustomAwayKitId,
                    CustomThirdKitId = c.Details.CustomThirdKitId,
                    CustomKeeperKitId = c.Details.CustomKeeperKitId,
                    KitColor1 = c.Details.KitColor1,
                    KitColor2 = c.Details.KitColor2,
                    KitColor3 = c.Details.KitColor3,
                    KitColor4 = c.Details.KitColor4,
                    KitAColor1 = c.Details.KitAColor1,
                    KitAColor2 = c.Details.KitAColor2,
                    KitAColor3 = c.Details.KitAColor3,
                    KitAColor4 = c.Details.KitAColor4,
                    KitThrdColor1 = c.Details.KitThrdColor1,
                    KitThrdColor2 = c.Details.KitThrdColor2,
                    KitThrdColor3 = c.Details.KitThrdColor3,
                    KitThrdColor4 = c.Details.KitThrdColor4,
                    DCustomKit = c.Details.DCustomKit,
                    CrestColor = c.Details.CrestColor,
                    CrestAssetId = c.Details.CrestAssetId
                }
            }).ToList(),
            Players = match.MatchPlayers.Select(p => new MatchPlayerDto
            {
                PlayerId = p.Player.PlayerId,
                ClubId = p.ClubId,
                Playername = p.Player.Playername,
                Goals = p.Goals,
                Assists = p.Assists,
                Rating = p.Rating,
                Cleansheetsany = p.Cleansheetsany,
                Cleansheetsdef = p.Cleansheetsdef,
                Cleansheetsgk = p.Cleansheetsgk,
                Losses = p.Losses,
                Mom = p.Mom,
                Passattempts = p.Passattempts,
                Passesmade = p.Passesmade,
                Realtimegame = p.Realtimegame,
                Realtimeidle = p.Realtimeidle,
                Redcards = p.Redcards,
                Saves = p.Saves,
                Score = p.Score,
                Shots = p.Shots,
                Tackleattempts = p.Tackleattempts,
                Tacklesmade = p.Tacklesmade,
                Vproattr = p.Vproattr,
                Vprohackreason = p.Vprohackreason,
                Wins = p.Wins,
                Pos = p.Pos,
                Namespace = p.Namespace,
                Stats = new PlayerMatchStatsDto
                {
                    Aceleracao = p.Player.PlayerMatchStats.Aceleracao,
                    Pique = p.Player.PlayerMatchStats.Pique,
                    Finalizacao = p.Player.PlayerMatchStats.Finalizacao,
                    Falta = p.Player.PlayerMatchStats.Falta,
                    Cabeceio = p.Player.PlayerMatchStats.Cabeceio,
                    ForcaDoChute = p.Player.PlayerMatchStats.ForcaDoChute,
                    ChuteLonge = p.Player.PlayerMatchStats.ChuteLonge,
                    Voleio = p.Player.PlayerMatchStats.Voleio,
                    Penalti = p.Player.PlayerMatchStats.Penalti,
                    Visao = p.Player.PlayerMatchStats.Visao,
                    Cruzamento = p.Player.PlayerMatchStats.Cruzamento,
                    Lancamento = p.Player.PlayerMatchStats.Lancamento,
                    PasseCurto = p.Player.PlayerMatchStats.PasseCurto,
                    Curva = p.Player.PlayerMatchStats.Curva,
                    Agilidade = p.Player.PlayerMatchStats.Agilidade,
                    Equilibrio = p.Player.PlayerMatchStats.Equilibrio,
                    PosAtaqueInutil = p.Player.PlayerMatchStats.PosAtaqueInutil,
                    ControleBola = p.Player.PlayerMatchStats.ControleBola,
                    Conducao = p.Player.PlayerMatchStats.Conducao,
                    Interceptacaos = p.Player.PlayerMatchStats.Interceptacaos,
                    NocaoDefensiva = p.Player.PlayerMatchStats.NocaoDefensiva,
                    DivididaEmPe = p.Player.PlayerMatchStats.DivididaEmPe,
                    Carrinho = p.Player.PlayerMatchStats.Carrinho,
                    Impulsao = p.Player.PlayerMatchStats.Impulsao,
                    Folego = p.Player.PlayerMatchStats.Folego,
                    Forca = p.Player.PlayerMatchStats.Forca,
                    Reacao = p.Player.PlayerMatchStats.Reacao,
                    Combatividade = p.Player.PlayerMatchStats.Combatividade,
                    Frieza = p.Player.PlayerMatchStats.Frieza,
                    ElasticidadeGL = p.Player.PlayerMatchStats.ElasticidadeGL,
                    ManejoGL = p.Player.PlayerMatchStats.ManejoGL,
                    ChuteGL = p.Player.PlayerMatchStats.ChuteGL,
                    ReflexosGL = p.Player.PlayerMatchStats.ReflexosGL,
                    PosGL = p.Player.PlayerMatchStats.PosGL
                }
            }).ToList()
        };

        return Ok(matchDtos);
    }

    [HttpGet("statistics/{matchId:long}")]
    public async Task<IActionResult> GetMatchStatisticsById(long matchId)
    {
        var match = await _dbContext.Matches
            .Include(m => m.Clubs)
                .ThenInclude(c => c.Details)
            .Include(m => m.MatchPlayers)
                .ThenInclude(mp => mp.Player)
            .FirstOrDefaultAsync(m => m.MatchId == matchId);

        if (match == null)
            return NotFound("Partida não encontrada.");

        var players = match.MatchPlayers;

        if (!players.Any())
            return Ok(new FullMatchStatisticsDto());

        int totalPlayers = players.Count;

        int totalGoals = players.Sum(p => p.Goals);
        int totalAssists = players.Sum(p => p.Assists);
        int totalShots = players.Sum(p => p.Shots);
        int totalPassesMade = players.Sum(p => p.Passesmade);
        int totalPassAttempts = players.Sum(p => p.Passattempts);
        int totalTacklesMade = players.Sum(p => p.Tacklesmade);
        int totalTackleAttempts = players.Sum(p => p.Tackleattempts);
        double totalRating = players.Sum(p => p.Rating);
        int totalWins = players.Sum(p => p.Wins);
        int totalLosses = players.Sum(p => p.Losses);
        int totalCleanSheets = players.Sum(p => p.Cleansheetsany);
        int totalRedCards = players.Sum(p => p.Redcards);
        int totalSaves = players.Sum(p => p.Saves);
        int totalMom = players.Count(p => p.Mom);
        int totalDraws = totalPlayers - totalWins - totalLosses;

        var overallStats = new MatchStatisticsDto
        {
            TotalMatches = 1,
            TotalPlayers = totalPlayers,
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

            AvgGoals = totalGoals / (double)totalPlayers,
            AvgAssists = totalAssists / (double)totalPlayers,
            AvgShots = totalShots / (double)totalPlayers,
            AvgPassesMade = totalPassesMade / (double)totalPlayers,
            AvgPassAttempts = totalPassAttempts / (double)totalPlayers,
            AvgTacklesMade = totalTacklesMade / (double)totalPlayers,
            AvgTackleAttempts = totalTackleAttempts / (double)totalPlayers,
            AvgRating = totalRating / totalPlayers,
            AvgRedCards = totalRedCards / (double)totalPlayers,
            AvgSaves = totalSaves / (double)totalPlayers,
            AvgMom = totalMom / (double)totalPlayers,

            WinPercent = (totalWins * 100.0) / totalPlayers,
            LossPercent = (totalLosses * 100.0) / totalPlayers,
            DrawPercent = (totalDraws * 100.0) / totalPlayers,
            CleanSheetsPercent = (totalCleanSheets * 100.0) / totalPlayers,
            MomPercent = (totalMom * 100.0) / totalPlayers,
            PassAccuracyPercent = totalPassAttempts > 0 ? (totalPassesMade * 100.0) / totalPassAttempts : 0,
            TackleSuccessPercent = totalTackleAttempts > 0 ? (totalTacklesMade * 100.0) / totalTackleAttempts : 0,
            GoalAccuracyPercent = totalShots > 0 ? (totalGoals * 100.0) / totalShots : 0
        };

        var playersStats = players
            .GroupBy(p => p.PlayerEntityId)
            .Select(g =>
            {
                var player = g.First().Player;
                int matches = g.Count();

                int goals = g.Sum(p => p.Goals);
                int shots = g.Sum(p => p.Shots);
                int passesMade = g.Sum(p => p.Passesmade);
                int passAttempts = g.Sum(p => p.Passattempts);
                int tacklesMade = g.Sum(p => p.Tacklesmade);
                int tackleAttempts = g.Sum(p => p.Tackleattempts);
                int wins = g.Sum(p => p.Wins);
                int losses = g.Sum(p => p.Losses);
                int draws = matches - wins - losses;

                return new PlayerStatisticsDto
                {
                    PlayerId = g.Key,
                    PlayerName = player?.Playername ?? "Unknown",
                    ClubId = player.ClubId,
                    MatchesPlayed = matches,
                    TotalGoals = goals,
                    TotalAssists = g.Sum(p => p.Assists),
                    TotalShots = shots,
                    TotalPassesMade = passesMade,
                    TotalPassAttempts = passAttempts,
                    TotalTacklesMade = tacklesMade,
                    TotalTackleAttempts = tackleAttempts,
                    TotalWins = wins,
                    TotalLosses = losses,
                    TotalDraws = draws,
                    TotalCleanSheets = g.Sum(p => p.Cleansheetsany),
                    TotalRedCards = g.Sum(p => p.Redcards),
                    TotalSaves = g.Sum(p => p.Saves),
                    TotalMom = g.Count(p => p.Mom),
                    AvgRating = g.Average(p => p.Rating),
                    PassAccuracyPercent = passAttempts > 0 ? (passesMade * 100.0) / passAttempts : 0,
                    TackleSuccessPercent = tackleAttempts > 0 ? (tacklesMade * 100.0) / tackleAttempts : 0,
                    GoalAccuracyPercent = shots > 0 ? (goals * 100.0) / shots : 0,
                    WinPercent = matches > 0 ? (wins * 100.0) / matches : 0
                };
            })
            .ToList();

        var clubsStats = players
            .GroupBy(p => p.ClubId)
            .Select(g =>
            {
                int matches = g.Count();
                int goals = g.Sum(p => p.Goals);
                int shots = g.Sum(p => p.Shots);
                int passesMade = g.Sum(p => p.Passesmade);
                int passAttempts = g.Sum(p => p.Passattempts);
                int wins = g.Sum(p => p.Wins);
                int losses = g.Sum(p => p.Losses);
                int draws = matches - wins - losses;

                // Obtém o clube do jogador e suas informações
                var club = _dbContext.MatchClubs.FirstOrDefault(c => c.Details.ClubId == g.Key);
                var clubName = club.Details.Name ?? $"Clube {g.Key}";
                var crestAssetId = club.Details.CrestAssetId;

                return new ClubStatisticsDto
                {
                    ClubId = g.Key,
                    ClubName = clubName, // Nome do clube
                    ClubCrestAssetId = crestAssetId,
                    MatchesPlayed = matches,
                    TotalGoals = goals,
                    TotalAssists = g.Sum(p => p.Assists),
                    TotalShots = shots,
                    TotalPassesMade = passesMade,
                    TotalPassAttempts = passAttempts,
                    TotalTacklesMade = g.Sum(p => p.Tacklesmade),
                    TotalTackleAttempts = g.Sum(p => p.Tackleattempts),
                    TotalWins = wins,
                    TotalLosses = losses,
                    TotalDraws = draws,
                    TotalCleanSheets = g.Sum(p => p.Cleansheetsany),
                    TotalRedCards = g.Sum(p => p.Redcards),
                    TotalSaves = g.Sum(p => p.Saves),
                    TotalMom = g.Count(p => p.Mom),
                    AvgRating = g.Average(p => p.Rating),
                    WinPercent = matches > 0 ? (wins * 100.0) / matches : 0,
                    PassAccuracyPercent = passAttempts > 0 ? (passesMade * 100.0) / passAttempts : 0,
                    GoalAccuracyPercent = shots > 0 ? (goals * 100.0) / shots : 0
                };
            })
            .ToList();

        return Ok(new FullMatchStatisticsDto
        {
            Overall = overallStats,
            Players = playersStats,
            Clubs = clubsStats
        });
    }


    [HttpGet("statistics/player/{matchId}/{playerId}")]
    public async Task<IActionResult> GetPlayerStatisticsByMatchAndPlayer(long matchId, long playerId)
    {
        var matchPlayer = await _dbContext.MatchPlayers
            .Include(mp => mp.Player)
                .ThenInclude(e => e.PlayerMatchStats)
            .FirstOrDefaultAsync(mp => mp.MatchId == matchId && mp.PlayerEntityId == playerId);

        if (matchPlayer == null)
            return NotFound($"Player with id {playerId} not found in match {matchId}.");

        var dto = new MatchPlayerStatsDto
        {
            PlayerId = matchPlayer.PlayerEntityId,
            PlayerName = matchPlayer.Player?.Playername ?? "Desconhecido",
            Assists = matchPlayer.Assists,
            CleansheetsAny = matchPlayer.Cleansheetsany,
            CleansheetsDef = matchPlayer.Cleansheetsdef,
            CleansheetsGk = matchPlayer.Cleansheetsgk,
            Goals = matchPlayer.Goals,
            GoalsConceded = matchPlayer.Goalsconceded,
            Losses = matchPlayer.Losses,
            Mom = matchPlayer.Mom,
            Namespace = matchPlayer.Namespace,
            PassAttempts = matchPlayer.Passattempts,
            PassesMade = matchPlayer.Passesmade,
            PassAccuracy = matchPlayer.Passattempts > 0
                ? (double)matchPlayer.Passesmade / matchPlayer.Passattempts * 100
                : 0,
            Position = matchPlayer.Pos,
            Rating = matchPlayer.Rating,
            RealtimeGame = matchPlayer.Realtimegame,
            RealtimeIdle = matchPlayer.Realtimeidle,
            RedCards = matchPlayer.Redcards,
            Saves = matchPlayer.Saves,
            Score = matchPlayer.Score,
            Shots = matchPlayer.Shots,
            TackleAttempts = matchPlayer.Tackleattempts,
            TacklesMade = matchPlayer.Tacklesmade,
            VproAttr = matchPlayer.Vproattr,
            VproHackReason = matchPlayer.Vprohackreason,
            Wins = matchPlayer.Wins,
            Statistics = matchPlayer.Player?.PlayerMatchStats != null
                ? new PlayerMatchStatsDto
                {
                    Aceleracao = matchPlayer.Player.PlayerMatchStats.Aceleracao,
                    Pique = matchPlayer.Player.PlayerMatchStats.Pique,
                    Finalizacao = matchPlayer.Player.PlayerMatchStats.Finalizacao,
                    Falta = matchPlayer.Player.PlayerMatchStats.Falta,
                    Cabeceio = matchPlayer.Player.PlayerMatchStats.Cabeceio,
                    ForcaDoChute = matchPlayer.Player.PlayerMatchStats.ForcaDoChute,
                    ChuteLonge = matchPlayer.Player.PlayerMatchStats.ChuteLonge,
                    Voleio = matchPlayer.Player.PlayerMatchStats.Voleio,
                    Penalti = matchPlayer.Player.PlayerMatchStats.Penalti,
                    Visao = matchPlayer.Player.PlayerMatchStats.Visao,
                    Cruzamento = matchPlayer.Player.PlayerMatchStats.Cruzamento,
                    Lancamento = matchPlayer.Player.PlayerMatchStats.Lancamento,
                    PasseCurto = matchPlayer.Player.PlayerMatchStats.PasseCurto,
                    Curva = matchPlayer.Player.PlayerMatchStats.Curva,
                    Agilidade = matchPlayer.Player.PlayerMatchStats.Agilidade,
                    Equilibrio = matchPlayer.Player.PlayerMatchStats.Equilibrio,
                    PosAtaqueInutil = matchPlayer.Player.PlayerMatchStats.PosAtaqueInutil,
                    ControleBola = matchPlayer.Player.PlayerMatchStats.ControleBola,
                    Conducao = matchPlayer.Player.PlayerMatchStats.Conducao,
                    Interceptacaos = matchPlayer.Player.PlayerMatchStats.Interceptacaos,
                    NocaoDefensiva = matchPlayer.Player.PlayerMatchStats.NocaoDefensiva,
                    DivididaEmPe = matchPlayer.Player.PlayerMatchStats.DivididaEmPe,
                    Carrinho = matchPlayer.Player.PlayerMatchStats.Carrinho,
                    Impulsao = matchPlayer.Player.PlayerMatchStats.Impulsao,
                    Folego = matchPlayer.Player.PlayerMatchStats.Folego,
                    Forca = matchPlayer.Player.PlayerMatchStats.Forca,
                    Reacao = matchPlayer.Player.PlayerMatchStats.Reacao,
                    Combatividade = matchPlayer.Player.PlayerMatchStats.Combatividade,
                    Frieza = matchPlayer.Player.PlayerMatchStats.Frieza,
                    ElasticidadeGL = matchPlayer.Player.PlayerMatchStats.ElasticidadeGL,
                    ManejoGL = matchPlayer.Player.PlayerMatchStats.ManejoGL,
                    ChuteGL = matchPlayer.Player.PlayerMatchStats.ChuteGL,
                    ReflexosGL = matchPlayer.Player.PlayerMatchStats.ReflexosGL,
                    PosGL = matchPlayer.Player.PlayerMatchStats.PosGL
                }
                : null
        };

        return Ok(dto);
    }

    [HttpGet("statistics/limited")]
    public async Task<IActionResult> GetMatchStatisticsLimited([FromQuery] int count = 10)
    {
        if (count <= 0)
            return BadRequest("O número de partidas deve ser maior que zero.");

        var matches = await _dbContext.Matches
            .Include(m => m.Clubs.Where(c => c.ClubId == 3463149))
                .ThenInclude(c => c.Details)
            .Include(m => m.MatchPlayers)
                .ThenInclude(mp => mp.Player)
            .Where(m => m.Clubs.Any(c => c.ClubId == 3463149))
            .OrderByDescending(m => m.Timestamp)
            .Take(count)
            .ToListAsync();

        if (!matches.Any())
            return Ok(new FullMatchStatisticsDto());

        var allPlayers = matches.SelectMany(m => m.MatchPlayers)
            .Where(e => e.Player.ClubId == 3463149)
            .ToList();

        if (!allPlayers.Any())
            return Ok(new FullMatchStatisticsDto());

        int totalPlayers = allPlayers.Select(p => p.PlayerEntityId).Distinct().Count();
        int totalMatchesPlayed = allPlayers.Count;

        // Agrupando por jogador
        var playersStats = allPlayers
            .GroupBy(p => p.PlayerEntityId)
            .Select(g =>
            {
                var player = g.First().Player;
                int matches = g.Count();
                int goals = g.Sum(p => p.Goals);
                int shots = g.Sum(p => p.Shots);
                int passesMade = g.Sum(p => p.Passesmade);
                int passAttempts = g.Sum(p => p.Passattempts);
                int tacklesMade = g.Sum(p => p.Tacklesmade);
                int tackleAttempts = g.Sum(p => p.Tackleattempts);
                int wins = g.Sum(p => p.Wins);
                int losses = g.Sum(p => p.Losses);
                int draws = matches - wins - losses;

                return new PlayerStatisticsDto
                {
                    PlayerId = g.Key,
                    PlayerName = player?.Playername ?? "Unknown",
                    ClubId = player.ClubId,
                    MatchesPlayed = matches,
                    TotalGoals = goals,
                    TotalAssists = g.Sum(p => p.Assists),
                    TotalShots = shots,
                    TotalPassesMade = passesMade,
                    TotalPassAttempts = passAttempts,
                    TotalTacklesMade = tacklesMade,
                    TotalTackleAttempts = tackleAttempts,
                    TotalWins = wins,
                    TotalLosses = losses,
                    TotalDraws = draws,
                    TotalCleanSheets = g.Sum(p => p.Cleansheetsany),
                    TotalRedCards = g.Sum(p => p.Redcards),
                    TotalSaves = g.Sum(p => p.Saves),
                    TotalMom = g.Count(p => p.Mom),
                    AvgRating = g.Average(p => p.Rating),
                    PassAccuracyPercent = passAttempts > 0 ? (passesMade * 100.0) / passAttempts : 0,
                    TackleSuccessPercent = tackleAttempts > 0 ? (tacklesMade * 100.0) / tackleAttempts : 0,
                    GoalAccuracyPercent = shots > 0 ? (goals * 100.0) / shots : 0,
                    WinPercent = matches > 0 ? (wins * 100.0) / matches : 0
                };
            })
            .OrderByDescending(p => p.MatchesPlayed)
            .ToList();

        // Agora, agregamos as estatísticas do clube a partir dos dados dos jogadores
        var clubsStats = allPlayers
            .GroupBy(p => p.ClubId)
            .Select(g =>
            {
                // Número de jogadores no clube
                int totalPlayers = g.Select(p => p.PlayerEntityId).Distinct().Count();

                // Estatísticas totais (somadas)
                int totalMatches = g.Count() / totalPlayers; // Total de partidas jogadas pelos jogadores
                int totalGoals = g.Sum(p => p.Goals);
                int totalShots = g.Sum(p => p.Shots);
                int totalPassesMade = g.Sum(p => p.Passesmade);
                int totalPassAttempts = g.Sum(p => p.Passattempts);
                int totalTacklesMade = g.Sum(p => p.Tacklesmade);
                int totalTackleAttempts = g.Sum(p => p.Tackleattempts);
                int totalWins = g.Sum(p => p.Wins);
                int totalLosses = g.Sum(p => p.Losses);

                // Calculando empates
                int totalDraws = totalMatches - totalWins - totalLosses;

                // Calculando as médias
                double avgWins = totalPlayers > 0 ? (double)totalWins / totalPlayers : 0;
                double avgLosses = totalPlayers > 0 ? (double)totalLosses / totalPlayers : 0;
                double avgDraws = totalMatches - avgWins - avgLosses;

                // Calculando percentuais de vitórias, derrotas e empates
                double winPercent = totalMatches > 0 ? (avgWins * 100.0) / totalMatches : 0;
                double lossPercent = totalMatches > 0 ? (avgLosses * 100.0) / totalMatches : 0;
                double drawPercent = totalMatches > 0 ? (avgDraws * 100.0) / totalMatches : 0;

                // Calculando a média de avaliações
                double avgRating = g.Average(p => p.Rating);

                // Calculando percentuais de passes e desarmes
                double passAccuracyPercent = totalPassAttempts > 0 ? (totalPassesMade * 100.0) / totalPassAttempts : 0;
                double tackleSuccessPercent = totalTackleAttempts > 0 ? (totalTacklesMade * 100.0) / totalTackleAttempts : 0;
                double goalAccuracyPercent = totalShots > 0 ? (totalGoals * 100.0) / totalShots : 0;

                return new ClubStatisticsDto
                {
                    ClubId = g.Key,
                    ClubName = $"Clube {g.Key}",
                    MatchesPlayed = totalMatches, // Total de partidas
                    TotalGoals = totalGoals,
                    TotalAssists = g.Sum(p => p.Assists),
                    TotalShots = totalShots,
                    TotalPassesMade = totalPassesMade,
                    TotalPassAttempts = totalPassAttempts,
                    TotalTacklesMade = totalTacklesMade,
                    TotalTackleAttempts = totalTackleAttempts,
                    TotalWins = avgWins, // Número de vitórias do clube
                    TotalLosses = avgLosses, // Número de derrotas
                    TotalDraws = avgDraws, // Número de empates
                    TotalCleanSheets = g.Sum(p => p.Cleansheetsany),
                    TotalRedCards = g.Sum(p => p.Redcards),
                    TotalSaves = g.Sum(p => p.Saves),
                    TotalMom = g.Count(p => p.Mom),
                    AvgRating = avgRating,
                    WinPercent = winPercent, // Percentual de vitórias
                    PassAccuracyPercent = passAccuracyPercent, // Percentual de precisão de passes
                    TackleSuccessPercent = tackleSuccessPercent, // Percentual de precisão de desarmes
                    GoalAccuracyPercent = goalAccuracyPercent // Percentual de precisão de gols
                };
            })
            .OrderByDescending(c => c.MatchesPlayed) // Ordena pelo número de partidas jogadas
            .ToList();


        // Agora, precisamos garantir que a soma de tentativas de passes, desarmes e chutes
        // sejam corretamente somadas para o cálculo geral do clube.
        int totalPassAttempts = clubsStats.Sum(c => c.TotalPassAttempts);
        int totalTackleAttempts = clubsStats.Sum(c => c.TotalTackleAttempts);
        int totalShots = clubsStats.Sum(c => c.TotalShots);

        // Estatísticas gerais (para o clube)
        var overallStats = new MatchStatisticsDto
        {
            TotalMatches = matches.Count,
            TotalPlayers = totalPlayers,
            TotalGoals = playersStats.Sum(p => p.TotalGoals),
            TotalAssists = playersStats.Sum(p => p.TotalAssists),
            TotalShots = totalShots, // Usando o valor somado de chutes
            TotalPassesMade = playersStats.Sum(p => p.TotalPassesMade),
            TotalPassAttempts = totalPassAttempts, // Usando o valor somado de tentativas de passes
            TotalTacklesMade = playersStats.Sum(p => p.TotalTacklesMade),
            TotalTackleAttempts = totalTackleAttempts, // Usando o valor somado de tentativas de desarmes
            TotalRating = playersStats.Sum(p => p.AvgRating),
            TotalWins = playersStats.Sum(p => p.TotalWins),
            TotalLosses = playersStats.Sum(p => p.TotalLosses),
            TotalDraws = playersStats.Sum(p => p.TotalDraws),
            TotalCleanSheets = playersStats.Sum(p => p.TotalCleanSheets),
            TotalRedCards = playersStats.Sum(p => p.TotalRedCards),
            TotalSaves = playersStats.Sum(p => p.TotalSaves),
            TotalMom = playersStats.Sum(p => p.TotalMom),

            WinPercent = totalMatchesPlayed > 0 ? (playersStats.Sum(p => p.TotalWins) * 100.0) / totalMatchesPlayed : 0,
            LossPercent = totalMatchesPlayed > 0 ? (playersStats.Sum(p => p.TotalLosses) * 100.0) / totalMatchesPlayed : 0,
            DrawPercent = totalMatchesPlayed > 0 ? (playersStats.Sum(p => p.TotalDraws) * 100.0) / totalMatchesPlayed : 0,
            CleanSheetsPercent = totalMatchesPlayed > 0 ? (playersStats.Sum(p => p.TotalCleanSheets) * 100.0) / totalMatchesPlayed : 0,
            MomPercent = totalMatchesPlayed > 0 ? (playersStats.Sum(p => p.TotalMom) * 100.0) / totalMatchesPlayed : 0,
            PassAccuracyPercent = totalPassAttempts > 0 ? (playersStats.Sum(p => p.TotalPassesMade) * 100.0) / totalPassAttempts : 0,
            TackleSuccessPercent = totalTackleAttempts > 0 ? (playersStats.Sum(p => p.TotalTacklesMade) * 100.0) / totalTackleAttempts : 0,
            GoalAccuracyPercent = totalShots > 0 ? (playersStats.Sum(p => p.TotalGoals) * 100.0) / totalShots : 0
        };

        // Retorna o resultado final
        return Ok(new FullMatchStatisticsDto
        {
            Overall = overallStats,
            Players = playersStats,
            Clubs = clubsStats
        });
    }


    [HttpGet("statistics")]
    public async Task<IActionResult> GetMatchStatistics()
    {
        var matches = await _dbContext.Matches
            .Include(m => m.Clubs.Where(c => c.ClubId == 3463149))
                .ThenInclude(c => c.Details)
            .Include(m => m.MatchPlayers)
                .ThenInclude(mp => mp.Player)
            .Where(m => m.Clubs.Any(c => c.ClubId == 3463149))
            .OrderByDescending(m => m.Timestamp)
            .ToListAsync();

        var allPlayers = matches.SelectMany(m => m.MatchPlayers)
            .Where(e => e.Player.ClubId == 3463149)
            .ToList();

        if (!allPlayers.Any())
            return Ok(new FullMatchStatisticsDto());

        int totalPlayers = allPlayers.Select(p => p.PlayerEntityId).Distinct().Count();

        int totalMatchesPlayed = allPlayers.Count;

        int totalGoals = allPlayers.Sum(p => p.Goals);
        int totalAssists = allPlayers.Sum(p => p.Assists);
        int totalShots = allPlayers.Sum(p => p.Shots);
        int totalPassesMade = allPlayers.Sum(p => p.Passesmade);
        int totalPassAttempts = allPlayers.Sum(p => p.Passattempts);
        int totalTacklesMade = allPlayers.Sum(p => p.Tacklesmade);
        int totalTackleAttempts = allPlayers.Sum(p => p.Tackleattempts);
        double totalRating = allPlayers.Sum(p => p.Rating);
        int totalWins = allPlayers.Sum(p => p.Wins);
        int totalLosses = allPlayers.Sum(p => p.Losses);
        int totalCleanSheets = allPlayers.Sum(p => p.Cleansheetsany);
        int totalRedCards = allPlayers.Sum(p => p.Redcards);
        int totalSaves = allPlayers.Sum(p => p.Saves);
        int totalMom = allPlayers.Count(p => p.Mom);

        int totalDraws = totalMatchesPlayed - totalWins - totalLosses;

        var overallStats = new MatchStatisticsDto
        {
            TotalMatches = matches.Count,
            TotalPlayers = totalPlayers,
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

            AvgGoals = totalMatchesPlayed > 0 ? totalGoals / (double)totalMatchesPlayed : 0,
            AvgAssists = totalMatchesPlayed > 0 ? totalAssists / (double)totalMatchesPlayed : 0,
            AvgShots = totalMatchesPlayed > 0 ? totalShots / (double)totalMatchesPlayed : 0,
            AvgPassesMade = totalMatchesPlayed > 0 ? totalPassesMade / (double)totalMatchesPlayed : 0,
            AvgPassAttempts = totalMatchesPlayed > 0 ? totalPassAttempts / (double)totalMatchesPlayed : 0,
            AvgTacklesMade = totalMatchesPlayed > 0 ? totalTacklesMade / (double)totalMatchesPlayed : 0,
            AvgTackleAttempts = totalMatchesPlayed > 0 ? totalTackleAttempts / (double)totalMatchesPlayed : 0,
            AvgRating = totalMatchesPlayed > 0 ? totalRating / totalMatchesPlayed : 0,
            AvgRedCards = totalMatchesPlayed > 0 ? totalRedCards / (double)totalMatchesPlayed : 0,
            AvgSaves = totalMatchesPlayed > 0 ? totalSaves / (double)totalMatchesPlayed : 0,
            AvgMom = totalMatchesPlayed > 0 ? totalMom / (double)totalMatchesPlayed : 0,

            WinPercent = totalMatchesPlayed > 0 ? (totalWins * 100.0) / totalMatchesPlayed : 0,
            LossPercent = totalMatchesPlayed > 0 ? (totalLosses * 100.0) / totalMatchesPlayed : 0,
            DrawPercent = totalMatchesPlayed > 0 ? (totalDraws * 100.0) / totalMatchesPlayed : 0,
            CleanSheetsPercent = totalMatchesPlayed > 0 ? (totalCleanSheets * 100.0) / totalMatchesPlayed : 0,
            MomPercent = totalMatchesPlayed > 0 ? (totalMom * 100.0) / totalMatchesPlayed : 0,
            PassAccuracyPercent = totalPassAttempts > 0 ? (totalPassesMade * 100.0) / totalPassAttempts : 0,
            TackleSuccessPercent = totalTackleAttempts > 0 ? (totalTacklesMade * 100.0) / totalTackleAttempts : 0,
            GoalAccuracyPercent = totalShots > 0 ? (totalGoals * 100.0) / totalShots : 0
        };

        var playersStats = allPlayers
            .GroupBy(p => p.PlayerEntityId)
            .Select(g =>
            {
                var player = g.First().Player;
                int matches = g.Count();

                int goals = g.Sum(p => p.Goals);
                int shots = g.Sum(p => p.Shots);
                int passesMade = g.Sum(p => p.Passesmade);
                int passAttempts = g.Sum(p => p.Passattempts);
                int tacklesMade = g.Sum(p => p.Tacklesmade);
                int tackleAttempts = g.Sum(p => p.Tackleattempts);
                int wins = g.Sum(p => p.Wins);
                int losses = g.Sum(p => p.Losses);
                int draws = matches - wins - losses;

                return new PlayerStatisticsDto
                {
                    PlayerId = g.Key,
                    PlayerName = player?.Playername ?? "Unknown",
                    ClubId = player.ClubId,
                    MatchesPlayed = matches,
                    TotalGoals = goals,
                    TotalAssists = g.Sum(p => p.Assists),
                    TotalShots = shots,
                    TotalPassesMade = passesMade,
                    TotalPassAttempts = passAttempts,
                    TotalTacklesMade = tacklesMade,
                    TotalTackleAttempts = tackleAttempts,
                    TotalWins = wins,
                    TotalLosses = losses,
                    TotalDraws = draws,
                    TotalCleanSheets = g.Sum(p => p.Cleansheetsany),
                    TotalRedCards = g.Sum(p => p.Redcards),
                    TotalSaves = g.Sum(p => p.Saves),
                    TotalMom = g.Count(p => p.Mom),
                    AvgRating = g.Average(p => p.Rating),
                    PassAccuracyPercent = passAttempts > 0 ? (passesMade * 100.0) / passAttempts : 0,
                    TackleSuccessPercent = tackleAttempts > 0 ? (tacklesMade * 100.0) / tackleAttempts : 0,
                    GoalAccuracyPercent = shots > 0 ? (goals * 100.0) / shots : 0,
                    WinPercent = matches > 0 ? (wins * 100.0) / matches : 0
                };
            })
            .OrderByDescending(p => p.MatchesPlayed)
            .ToList();

        var clubsStats = allPlayers
            .GroupBy(p => p.ClubId)
            .Select(g =>
            {
                int matches = g.Count();
                int goals = g.Sum(p => p.Goals);
                int shots = g.Sum(p => p.Shots);
                int passesMade = g.Sum(p => p.Passesmade);
                int passAttempts = g.Sum(p => p.Passattempts);
                int wins = g.Sum(p => p.Wins);
                int losses = g.Sum(p => p.Losses);
                int draws = matches - wins - losses;

                return new ClubStatisticsDto
                {
                    ClubId = g.Key,
                    ClubName = $"Clube {g.Key}",
                    MatchesPlayed = matches,
                    TotalGoals = goals,
                    TotalAssists = g.Sum(p => p.Assists),
                    TotalShots = shots,
                    TotalPassesMade = passesMade,
                    TotalPassAttempts = passAttempts,
                    TotalTacklesMade = g.Sum(p => p.Tacklesmade),
                    TotalTackleAttempts = g.Sum(p => p.Tackleattempts),
                    TotalWins = wins,
                    TotalLosses = losses,
                    TotalDraws = draws,
                    TotalCleanSheets = g.Sum(p => p.Cleansheetsany),
                    TotalRedCards = g.Sum(p => p.Redcards),
                    TotalSaves = g.Sum(p => p.Saves),
                    TotalMom = g.Count(p => p.Mom),
                    AvgRating = g.Average(p => p.Rating),
                    WinPercent = matches > 0 ? (wins * 100.0) / matches : 0,
                    PassAccuracyPercent = passAttempts > 0 ? (passesMade * 100.0) / passAttempts : 0,
                    GoalAccuracyPercent = shots > 0 ? (goals * 100.0) / shots : 0
                };
            })
            .OrderByDescending(c => c.MatchesPlayed)
            .ToList();

        return Ok(new FullMatchStatisticsDto
        {
            Overall = overallStats,
            Players = playersStats,
            Clubs = clubsStats
        });
    }


    [HttpGet("matches/results")]
    public async Task<IActionResult> GetMatchResults()
    {
        var matches = await _dbContext.Matches
            .Where(m => m.Clubs.Any(c => c.ClubId == 3463149))  // Filtra partidas onde pelo menos um clube tem o ClubId 3463149
            .Include(m => m.Clubs)  // Inclui ambos os clubes
                .ThenInclude(c => c.Details)  // Inclui os detalhes dos clubes
            .OrderByDescending(m => m.Timestamp)  // Ordena pelas partidas mais recentes
            .ToListAsync();

        var resultList = new List<MatchResultDto>();

        foreach (var match in matches)
        {
            var clubs = match.Clubs
                .OrderBy(c => c.Team) // Garantir uma ordem consistente
                .ToList();

            if (clubs.Count != 2)
                continue; // Ignorar partidas incompletas

            var clubA = clubs[0];
            var clubB = clubs[1];

            var dto = new MatchResultDto
            {
                MatchId = match.MatchId,
                Timestamp = match.Timestamp,
                ClubAName = clubA.Details?.Name ?? $"Clube {clubA.ClubId}",
                ClubAGoals = clubA.Goals,
                ClubADetails = clubA.Details == null ? null : new ClubDetailsDto
                {
                    Name = clubA.Details.Name,
                    RegionId = clubA.Details.RegionId,
                    TeamId = clubA.Details.TeamId,
                    StadName = clubA.Details.StadName,
                    KitId = clubA.Details.KitId,
                    CustomKitId = clubA.Details.CustomKitId,
                    CustomAwayKitId = clubA.Details.CustomAwayKitId,
                    CustomThirdKitId = clubA.Details.CustomThirdKitId,
                    CustomKeeperKitId = clubA.Details.CustomKeeperKitId,
                    KitColor1 = clubA.Details.KitColor1,
                    KitColor2 = clubA.Details.KitColor2,
                    KitColor3 = clubA.Details.KitColor3,
                    KitColor4 = clubA.Details.KitColor4,
                    KitAColor1 = clubA.Details.KitAColor1,
                    KitAColor2 = clubA.Details.KitAColor2,
                    KitAColor3 = clubA.Details.KitAColor3,
                    KitAColor4 = clubA.Details.KitAColor4,
                    KitThrdColor1 = clubA.Details.KitThrdColor1,
                    KitThrdColor2 = clubA.Details.KitThrdColor2,
                    KitThrdColor3 = clubA.Details.KitThrdColor3,
                    KitThrdColor4 = clubA.Details.KitThrdColor4,
                    DCustomKit = clubA.Details.DCustomKit,
                    CrestColor = clubA.Details.CrestColor,
                    CrestAssetId = clubA.Details.CrestAssetId
                },
                ClubBName = clubB.Details?.Name ?? $"Clube {clubB.ClubId}",
                ClubBGoals = clubB.Goals,
                ClubBDetails = clubB.Details == null ? null : new ClubDetailsDto
                {
                    Name = clubB.Details.Name,
                    RegionId = clubB.Details.RegionId,
                    TeamId = clubB.Details.TeamId,
                    StadName = clubB.Details.StadName,
                    KitId = clubB.Details.KitId,
                    CustomKitId = clubB.Details.CustomKitId,
                    CustomAwayKitId = clubB.Details.CustomAwayKitId,
                    CustomThirdKitId = clubB.Details.CustomThirdKitId,
                    CustomKeeperKitId = clubB.Details.CustomKeeperKitId,
                    KitColor1 = clubB.Details.KitColor1,
                    KitColor2 = clubB.Details.KitColor2,
                    KitColor3 = clubB.Details.KitColor3,
                    KitColor4 = clubB.Details.KitColor4,
                    KitAColor1 = clubB.Details.KitAColor1,
                    KitAColor2 = clubB.Details.KitAColor2,
                    KitAColor3 = clubB.Details.KitAColor3,
                    KitAColor4 = clubB.Details.KitAColor4,
                    KitThrdColor1 = clubB.Details.KitThrdColor1,
                    KitThrdColor2 = clubB.Details.KitThrdColor2,
                    KitThrdColor3 = clubB.Details.KitThrdColor3,
                    KitThrdColor4 = clubB.Details.KitThrdColor4,
                    DCustomKit = clubB.Details.DCustomKit,
                    CrestColor = clubB.Details.CrestColor,
                    CrestAssetId = clubB.Details.CrestAssetId
                },
                ResultText = $"{clubA.Details?.Name ?? "Clube A"} {clubA.Goals} x {clubB.Goals} {clubB.Details?.Name ?? "Clube B"}"
            };

            resultList.Add(dto);
        }

        return Ok(resultList);
    }

    [HttpGet("{playerId:long}")]
    public async Task<IActionResult> GetPlayerById(long playerId)
    {
        var player = await _dbContext.Players.FirstOrDefaultAsync(p => p.PlayerId == playerId);

        if (player == null)
            return NotFound();

        return Ok(player);
    }

[HttpDelete("{matchId}")]
public async Task<IActionResult> DeleteMatch(long matchId)
{
    var match = await _dbContext.Matches
        .Include(m => m.MatchPlayers)
        .Include(m => m.Clubs)
        .FirstOrDefaultAsync(m => m.MatchId == matchId);

    if (match == null)
    {
        return NotFound(new { message = "Partida não encontrada" });
    }

    var matchPlayers = await _dbContext.MatchPlayers
        .Where(mp => mp.MatchId == matchId)
        .ToListAsync();

    var playerMatchStatsIds = matchPlayers.Select(mp => mp.PlayerMatchStatsEntityId).ToList();
    var playerMatchStats = await _dbContext.PlayerMatchStats
        .Where(pms => playerMatchStatsIds.Contains(pms.Id))
        .ToListAsync();

    _dbContext.PlayerMatchStats.RemoveRange(playerMatchStats);
    _dbContext.MatchPlayers.RemoveRange(matchPlayers);

    var matchClubs = await _dbContext.MatchClubs
        .Where(mc => mc.MatchId == matchId)
        .ToListAsync();

    _dbContext.MatchClubs.RemoveRange(matchClubs);

    _dbContext.Matches.Remove(match);

    await _dbContext.SaveChangesAsync();

    return NoContent();
}
}