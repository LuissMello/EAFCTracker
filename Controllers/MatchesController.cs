using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

[ApiController]
[Route("api/[controller]")]
public class MatchesController : ControllerBase
{
    private const int MinOpponentPlayers = 2;
    private const int MaxOpponentPlayers = 11;

    private readonly EAFCContext _db;

    public MatchesController(EAFCContext dbContext) => _db = dbContext;

    // ==============================
    // Projeções / Mapeadores (SRP)
    // ==============================
    private static class Proj
    {
        public static readonly Expression<Func<MatchPlayerEntity, MatchPlayerStatsDto>> MatchPlayerStatsRow =
        mp => new MatchPlayerStatsDto
        {
            PlayerId = mp.PlayerEntityId,
            PlayerName = mp.Player != null ? mp.Player.Playername : "Desconhecido",
            // campos diretos do MatchPlayer
            Assists = mp.Assists,
            CleansheetsAny = mp.Cleansheetsany,
            CleansheetsDef = mp.Cleansheetsdef,
            CleansheetsGk = mp.Cleansheetsgk,
            Goals = mp.Goals,
            GoalsConceded = mp.Goalsconceded,
            Losses = mp.Losses,
            Mom = mp.Mom,
            Namespace = mp.Namespace,
            PassAttempts = mp.Passattempts,
            PassesMade = mp.Passesmade,
            // calcula % no servidor
            PassAccuracy = mp.Passattempts > 0 ? (double)mp.Passesmade / mp.Passattempts * 100.0 : 0.0,
            Position = mp.Pos,
            Rating = mp.Rating,
            RealtimeGame = mp.Realtimegame,
            RealtimeIdle = mp.Realtimeidle,
            RedCards = mp.Redcards,
            Saves = mp.Saves,
            Score = mp.Score,
            Shots = mp.Shots,
            TackleAttempts = mp.Tackleattempts,
            TacklesMade = mp.Tacklesmade,
            VproAttr = mp.Vproattr,
            VproHackReason = mp.Vprohackreason,
            Wins = mp.Wins,
            // stats agregadas do jogador (se existirem)
            Statistics = mp.Player != null && mp.Player.PlayerMatchStats != null
                ? new PlayerMatchStatsDto
                {
                    Aceleracao = mp.Player.PlayerMatchStats.Aceleracao,
                    Pique = mp.Player.PlayerMatchStats.Pique,
                    Finalizacao = mp.Player.PlayerMatchStats.Finalizacao,
                    Falta = mp.Player.PlayerMatchStats.Falta,
                    Cabeceio = mp.Player.PlayerMatchStats.Cabeceio,
                    ForcaDoChute = mp.Player.PlayerMatchStats.ForcaDoChute,
                    ChuteLonge = mp.Player.PlayerMatchStats.ChuteLonge,
                    Voleio = mp.Player.PlayerMatchStats.Voleio,
                    Penalti = mp.Player.PlayerMatchStats.Penalti,
                    Visao = mp.Player.PlayerMatchStats.Visao,
                    Cruzamento = mp.Player.PlayerMatchStats.Cruzamento,
                    Lancamento = mp.Player.PlayerMatchStats.Lancamento,
                    PasseCurto = mp.Player.PlayerMatchStats.PasseCurto,
                    Curva = mp.Player.PlayerMatchStats.Curva,
                    Agilidade = mp.Player.PlayerMatchStats.Agilidade,
                    Equilibrio = mp.Player.PlayerMatchStats.Equilibrio,
                    PosAtaqueInutil = mp.Player.PlayerMatchStats.PosAtaqueInutil,
                    ControleBola = mp.Player.PlayerMatchStats.ControleBola,
                    Conducao = mp.Player.PlayerMatchStats.Conducao,
                    Interceptacaos = mp.Player.PlayerMatchStats.Interceptacaos,
                    NocaoDefensiva = mp.Player.PlayerMatchStats.NocaoDefensiva,
                    DivididaEmPe = mp.Player.PlayerMatchStats.DivididaEmPe,
                    Carrinho = mp.Player.PlayerMatchStats.Carrinho,
                    Impulsao = mp.Player.PlayerMatchStats.Impulsao,
                    Folego = mp.Player.PlayerMatchStats.Folego,
                    Forca = mp.Player.PlayerMatchStats.Forca,
                    Reacao = mp.Player.PlayerMatchStats.Reacao,
                    Combatividade = mp.Player.PlayerMatchStats.Combatividade,
                    Frieza = mp.Player.PlayerMatchStats.Frieza,
                    ElasticidadeGL = mp.Player.PlayerMatchStats.ElasticidadeGL,
                    ManejoGL = mp.Player.PlayerMatchStats.ManejoGL,
                    ChuteGL = mp.Player.PlayerMatchStats.ChuteGL,
                    ReflexosGL = mp.Player.PlayerMatchStats.ReflexosGL,
                    PosGL = mp.Player.PlayerMatchStats.PosGL
                }
                : null
        };


        public static readonly Expression<Func<PlayerMatchStatsEntity, PlayerMatchStatsDto>> PlayerMatchStats =
            s => new PlayerMatchStatsDto
            {
                Aceleracao = s.Aceleracao,
                Pique = s.Pique,
                Finalizacao = s.Finalizacao,
                Falta = s.Falta,
                Cabeceio = s.Cabeceio,
                ForcaDoChute = s.ForcaDoChute,
                ChuteLonge = s.ChuteLonge,
                Voleio = s.Voleio,
                Penalti = s.Penalti,
                Visao = s.Visao,
                Cruzamento = s.Cruzamento,
                Lancamento = s.Lancamento,
                PasseCurto = s.PasseCurto,
                Curva = s.Curva,
                Agilidade = s.Agilidade,
                Equilibrio = s.Equilibrio,
                PosAtaqueInutil = s.PosAtaqueInutil,
                ControleBola = s.ControleBola,
                Conducao = s.Conducao,
                Interceptacaos = s.Interceptacaos,
                NocaoDefensiva = s.NocaoDefensiva,
                DivididaEmPe = s.DivididaEmPe,
                Carrinho = s.Carrinho,
                Impulsao = s.Impulsao,
                Folego = s.Folego,
                Forca = s.Forca,
                Reacao = s.Reacao,
                Combatividade = s.Combatividade,
                Frieza = s.Frieza, // ajuste se o nome for Frieza
                ElasticidadeGL = s.ElasticidadeGL,
                ManejoGL = s.ManejoGL,
                ChuteGL = s.ChuteGL,
                ReflexosGL = s.ReflexosGL,
                PosGL = s.PosGL
            };

        public static readonly Expression<Func<ClubDetailsEntity, ClubDetailsDto>> ClubDetails =
            d => new ClubDetailsDto
            {
                Name = d.Name,
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
                ClubId = d.ClubId // se existir no DTO
            };

        public static readonly Expression<Func<MatchPlayerEntity, MatchPlayerDto>> MatchPlayer =
            p => new MatchPlayerDto
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
                Stats = p.Player.PlayerMatchStats == null ? null : new PlayerMatchStatsDto
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
            };

        public static readonly Expression<Func<MatchClubEntity, MatchClubDto>> MatchClub =
            c => new MatchClubDto
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
                    CrestAssetId = c.Details.CrestAssetId,
                    SelectedKitType = c.Details.SelectedKitType,
                    ClubId = c.Details.ClubId
                }
            };

        public static readonly Expression<Func<MatchEntity, MatchDto>> Match =
            m => new MatchDto
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
                        CrestAssetId = c.Details.CrestAssetId,
                        SelectedKitType = c.Details.SelectedKitType,
                        ClubId = c.Details.ClubId
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
                    Stats = p.Player.PlayerMatchStats == null ? null : new PlayerMatchStatsDto
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
    }

    // ==============================
    // Helpers
    // ==============================
    private static int ClampOpp(int value) =>
        Math.Min(MaxOpponentPlayers, Math.Max(MinOpponentPlayers, value));

    private static int? ReadOppAliasOrNull(HttpRequest req, int? opponentCount)
    {
        if (opponentCount.HasValue) return opponentCount;

        return req.Query.TryGetValue("opp", out var v) && int.TryParse(v, out var parsed)
            ? parsed
            : (int?)null;
    }

    // ==============================
    // Endpoints
    // ==============================

    [HttpGet]
    public async Task<IActionResult> GetAllMatches(CancellationToken ct)
    {
        var matches = await _db.Matches
            .AsNoTracking()
            .OrderByDescending(m => m.Timestamp)
            .Select(Proj.Match)                 // projeção server-side (sem Include pesado)
            .ToListAsync(ct);

        return Ok(matches);
    }

    [HttpGet("{matchId:long}")]
    public async Task<IActionResult> GetMatchById(long matchId, CancellationToken ct)
    {
        var dto = await _db.Matches
            .AsNoTracking()
            .Where(m => m.MatchId == matchId)
            .Select(Proj.Match)
            .FirstOrDefaultAsync(ct);

        if (dto is null) return NotFound();
        return Ok(dto);
    }

    [HttpGet("statistics/{matchId:long}")]
    public async Task<IActionResult> GetMatchStatisticsById(long matchId, CancellationToken ct)
    {
        // Carrega uma única partida + players + clubs (no-Tracking)
        var match = await _db.Matches
            .AsNoTracking()
            .Include(m => m.Clubs).ThenInclude(c => c.Details)
            .Include(m => m.MatchPlayers).ThenInclude(mp => mp.Player)
            .FirstOrDefaultAsync(m => m.MatchId == matchId, ct);

        if (match == null) return NotFound("Partida não encontrada.");

        var players = match.MatchPlayers;
        if (players.Count == 0) return Ok(new FullMatchStatisticsDto());

        // Lookup para evitar N+1 ao buscar nomes/escudos
        var clubsById = match.Clubs.ToDictionary(c => c.ClubId, c => c);

        // agregações simples (uma partida)
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

        var overall = new MatchStatisticsDto
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
                var first = g.First();
                var player = first.Player;
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
                var c = clubsById.TryGetValue(g.Key, out var club) ? club : null;
                int matches = g.Count();
                int goals = g.Sum(p => p.Goals);
                int shots = g.Sum(p => p.Shots);
                int passesMade = g.Sum(p => p.Passesmade);
                int passAttempts = g.Sum(p => p.Passattempts);
                int tacklesAttempts = g.Sum(p => p.Tackleattempts);
                int tacklesMade = g.Sum(p => p.Tacklesmade);
                int wins = g.Sum(p => p.Wins);
                int losses = g.Sum(p => p.Losses);
                int draws = matches - wins - losses;

                return new ClubStatisticsDto
                {
                    ClubId = g.Key,
                    ClubName = c?.Details?.Name ?? $"Clube {g.Key}",
                    ClubCrestAssetId = c?.Details?.CrestAssetId,
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
                    TackleSuccessPercent = tacklesAttempts > 0 ? (tacklesMade * 100.0) / tacklesAttempts : 0,
                    GoalAccuracyPercent = shots > 0 ? (goals * 100.0) / shots : 0
                };
            })
            .ToList();

        return Ok(new FullMatchStatisticsDto
        {
            Overall = overall,
            Players = playersStats,
            Clubs = clubsStats
        });
    }

    [HttpGet("statistics/limited")]
    public async Task<IActionResult> GetMatchStatisticsLimited(
        [FromQuery] long clubId,
        [FromQuery] int? opponentCount,
        [FromQuery] int count = 10,
        CancellationToken ct = default)
    {
        if (clubId <= 0) return BadRequest("Informe um clubId válido.");
        if (count <= 0) return BadRequest("O número de partidas deve ser maior que zero.");

        opponentCount = ReadOppAliasOrNull(Request, opponentCount);
        if (opponentCount.HasValue)
        {
            opponentCount = ClampOpp(opponentCount.Value);
            if (opponentCount < MinOpponentPlayers || opponentCount > MaxOpponentPlayers)
                return BadRequest($"opponentCount deve estar entre {MinOpponentPlayers} e {MaxOpponentPlayers}.");
        }

        // Query base
        IQueryable<MatchEntity> query = _db.Matches
            .AsNoTracking()
            .Include(m => m.Clubs.Where(c => c.ClubId == clubId)).ThenInclude(c => c.Details)
            .Include(m => m.MatchPlayers).ThenInclude(mp => mp.Player)
            .Where(m => m.Clubs.Any(c => c.ClubId == clubId));

        // Filtro por quantidade de jogadores do adversário
        if (opponentCount.HasValue)
        {
            int oc = opponentCount.Value;
            query = query.Where(m =>
                m.MatchPlayers
                 .Where(mp => mp.Player.ClubId != clubId)
                 .Select(mp => mp.PlayerEntityId)
                 .Distinct()
                 .Count() == oc);
        }

        var matches = await query
            .OrderByDescending(m => m.Timestamp)
            .Take(count)
            .ToListAsync(ct);

        if (matches.Count == 0) return Ok(new FullMatchStatisticsDto());

        var allPlayers = matches.SelectMany(m => m.MatchPlayers)
                                .Where(e => e.Player.ClubId == clubId)
                                .ToList();

        if (allPlayers.Count == 0) return Ok(new FullMatchStatisticsDto());

        // Base correta para W/D/L
        var clubSides = matches.SelectMany(m => m.Clubs).Where(c => c.ClubId == clubId).ToList();

        int matchesPlayedByClub = clubSides.Count;
        int winsCount = clubSides.Count(c => c.Goals > c.GoalsAgainst);
        int lossesCount = clubSides.Count(c => c.Goals < c.GoalsAgainst);
        int drawsCount = matchesPlayedByClub - winsCount - lossesCount;
        int cleanSheetsMatches = clubSides.Count(c => c.GoalsAgainst == 0);

        int momMatches = matches.Count(m => m.MatchPlayers.Any(mp => mp.Player.ClubId == clubId && mp.Mom));
        int distinctPlayersCount = allPlayers.Select(p => p.PlayerEntityId).Distinct().Count();

        var playersStats = allPlayers
            .GroupBy(p => p.PlayerEntityId)
            .Select(g =>
            {
                var player = g.First().Player;
                int matchesPlayed = g.Count();
                int goals = g.Sum(p => p.Goals);
                int shots = g.Sum(p => p.Shots);
                int passesMade = g.Sum(p => p.Passesmade);
                int passAttempts = g.Sum(p => p.Passattempts);
                int tacklesMade = g.Sum(p => p.Tacklesmade);
                int tackleAttempts = g.Sum(p => p.Tackleattempts);
                int wins = g.Sum(p => p.Wins);
                int losses = g.Sum(p => p.Losses);
                int draws = matchesPlayed - wins - losses;

                return new PlayerStatisticsDto
                {
                    PlayerId = g.Key,
                    PlayerName = player?.Playername ?? "Unknown",
                    ClubId = player.ClubId,
                    MatchesPlayed = matchesPlayed,
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
                    WinPercent = matchesPlayed > 0 ? (wins * 100.0) / matchesPlayed : 0
                };
            })
            .OrderByDescending(p => p.MatchesPlayed)
            .ToList();

        int totalShots = allPlayers.Sum(p => p.Shots);
        int totalPassesMade = allPlayers.Sum(p => p.Passesmade);
        int totalPassAttempts = allPlayers.Sum(p => p.Passattempts);
        int totalTacklesMade = allPlayers.Sum(p => p.Tacklesmade);
        int totalTackleAttempts = allPlayers.Sum(p => p.Tackleattempts);
        int totalGoals = allPlayers.Sum(p => p.Goals);

        var firstSide = clubSides.FirstOrDefault();
        string clubName = firstSide?.Details?.Name ?? $"Clube {clubId}";
        string? crestAssetId = firstSide?.Details?.CrestAssetId;

        var clubsStats = new List<ClubStatisticsDto>
        {
            new ClubStatisticsDto
            {
                ClubId = (int)clubId,
                ClubName = clubName,
                ClubCrestAssetId = crestAssetId,
                MatchesPlayed = matchesPlayedByClub,
                TotalGoals = totalGoals,
                TotalAssists = allPlayers.Sum(p => p.Assists),
                TotalShots = totalShots,
                TotalPassesMade = totalPassesMade,
                TotalPassAttempts = totalPassAttempts,
                TotalTacklesMade = totalTacklesMade,
                TotalTackleAttempts = totalTackleAttempts,
                TotalWins = winsCount,
                TotalLosses = lossesCount,
                TotalDraws = drawsCount,
                TotalCleanSheets = cleanSheetsMatches,
                TotalRedCards = allPlayers.Sum(p => p.Redcards),
                TotalSaves = allPlayers.Sum(p => p.Saves),
                TotalMom = momMatches,
                AvgRating = allPlayers.Any() ? allPlayers.Average(p => p.Rating) : 0,
                WinPercent = matchesPlayedByClub > 0 ? (winsCount * 100.0) / matchesPlayedByClub : 0,
                PassAccuracyPercent = totalPassAttempts > 0 ? (totalPassesMade * 100.0) / totalPassAttempts : 0,
                TackleSuccessPercent = totalTackleAttempts > 0 ? (totalTacklesMade * 100.0) / totalTackleAttempts : 0,
                GoalAccuracyPercent = totalShots > 0 ? (totalGoals * 100.0) / totalShots : 0
            }
        };

        var overall = new MatchStatisticsDto
        {
            TotalMatches = matchesPlayedByClub,
            TotalPlayers = distinctPlayersCount,
            TotalGoals = playersStats.Sum(p => p.TotalGoals),
            TotalAssists = playersStats.Sum(p => p.TotalAssists),
            TotalShots = totalShots,
            TotalPassesMade = playersStats.Sum(p => p.TotalPassesMade),
            TotalPassAttempts = totalPassAttempts,
            TotalTacklesMade = playersStats.Sum(p => p.TotalTacklesMade),
            TotalTackleAttempts = totalTackleAttempts,
            TotalRating = playersStats.Sum(p => p.AvgRating),
            TotalWins = winsCount,
            TotalLosses = lossesCount,
            TotalDraws = drawsCount,
            TotalCleanSheets = cleanSheetsMatches,
            TotalRedCards = playersStats.Sum(p => p.TotalRedCards),
            TotalSaves = playersStats.Sum(p => p.TotalSaves),
            TotalMom = momMatches,
            WinPercent = matchesPlayedByClub > 0 ? (winsCount * 100.0) / matchesPlayedByClub : 0,
            LossPercent = matchesPlayedByClub > 0 ? (lossesCount * 100.0) / matchesPlayedByClub : 0,
            DrawPercent = matchesPlayedByClub > 0 ? (drawsCount * 100.0) / matchesPlayedByClub : 0,
            CleanSheetsPercent = matchesPlayedByClub > 0 ? (cleanSheetsMatches * 100.0) / matchesPlayedByClub : 0,
            MomPercent = matchesPlayedByClub > 0 ? (momMatches * 100.0) / matchesPlayedByClub : 0,
            PassAccuracyPercent = totalPassAttempts > 0 ? (playersStats.Sum(p => p.TotalPassesMade) * 100.0) / totalPassAttempts : 0,
            TackleSuccessPercent = totalTackleAttempts > 0 ? (playersStats.Sum(p => p.TotalTacklesMade) * 100.0) / totalTackleAttempts : 0,
            GoalAccuracyPercent = totalShots > 0 ? (playersStats.Sum(p => p.TotalGoals) * 100.0) / totalShots : 0
        };

        return Ok(new FullMatchStatisticsDto { Overall = overall, Players = playersStats, Clubs = clubsStats });
    }

    [HttpGet("statistics")]
    public async Task<IActionResult> GetMatchStatistics([FromQuery] long clubId, CancellationToken ct)
    {
        if (clubId <= 0) return BadRequest("Informe um clubId válido.");

        // carrega todas as partidas do clube com no-tracking
        var matches = await _db.Matches
            .AsNoTracking()
            .Include(m => m.Clubs.Where(c => c.ClubId == clubId)).ThenInclude(c => c.Details)
            .Include(m => m.MatchPlayers).ThenInclude(mp => mp.Player)
            .Where(m => m.Clubs.Any(c => c.ClubId == clubId))
            .OrderByDescending(m => m.Timestamp)
            .ToListAsync(ct);

        var allPlayers = matches.SelectMany(m => m.MatchPlayers)
                                .Where(e => e.Player.ClubId == clubId)
                                .ToList();

        if (allPlayers.Count == 0) return Ok(new FullMatchStatisticsDto());

        // lookup de clubs para evitar N+1
        var clubsById = matches.SelectMany(m => m.Clubs).ToDictionary(c => c.ClubId, c => c);

        int distinctPlayers = allPlayers.Select(p => p.PlayerEntityId).Distinct().Count();
        int totalRows = allPlayers.Count;

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
        int totalDraws = totalRows - totalWins - totalLosses;

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
            WinPercent = totalRows > 0 ? (totalWins * 100.0) / totalRows : 0,
            LossPercent = totalRows > 0 ? (totalLosses * 100.0) / totalRows : 0,
            DrawPercent = totalRows > 0 ? (totalDraws * 100.0) / totalRows : 0,
            CleanSheetsPercent = totalRows > 0 ? (totalCleanSheets * 100.0) / totalRows : 0,
            MomPercent = totalRows > 0 ? (totalMom * 100.0) / totalRows : 0,
            PassAccuracyPercent = totalPassAttempts > 0 ? (totalPassesMade * 100.0) / totalPassAttempts : 0,
            TackleSuccessPercent = totalTackleAttempts > 0 ? (totalTacklesMade * 100.0) / totalTackleAttempts : 0,
            GoalAccuracyPercent = totalShots > 0 ? (totalGoals * 100.0) / totalShots : 0
        };

        var playersStats = allPlayers
            .GroupBy(p => p.PlayerEntityId)
            .Select(g =>
            {
                var player = g.First().Player;
                int matchesPlayed = g.Count();
                int goals = g.Sum(p => p.Goals);
                int shots = g.Sum(p => p.Shots);
                int passesMade = g.Sum(p => p.Passesmade);
                int passAttempts = g.Sum(p => p.Passattempts);
                int tacklesMade = g.Sum(p => p.Tacklesmade);
                int tackleAttempts = g.Sum(p => p.Tackleattempts);
                int wins = g.Sum(p => p.Wins);
                int losses = g.Sum(p => p.Losses);
                int draws = matchesPlayed - wins - losses;

                return new PlayerStatisticsDto
                {
                    PlayerId = g.Key,
                    PlayerName = player?.Playername ?? "Unknown",
                    ClubId = player.ClubId,
                    MatchesPlayed = matchesPlayed,
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
                    WinPercent = matchesPlayed > 0 ? (wins * 100.0) / matchesPlayed : 0
                };
            })
            .OrderByDescending(p => p.MatchesPlayed)
            .ToList();

        var clubsStats = allPlayers
            .GroupBy(p => p.ClubId)
            .Select(g =>
            {
                var c = clubsById.TryGetValue(g.Key, out var club) ? club : null;
                int matchesPlayed = g.Count();
                int goals = g.Sum(p => p.Goals);
                int shots = g.Sum(p => p.Shots);
                int passesMade = g.Sum(p => p.Passesmade);
                int passAttempts = g.Sum(p => p.Passattempts);
                int wins = g.Sum(p => p.Wins);
                int losses = g.Sum(p => p.Losses);
                int draws = matchesPlayed - wins - losses;

                return new ClubStatisticsDto
                {
                    ClubId = g.Key,
                    ClubName = c?.Details?.Name ?? $"Clube {g.Key}",
                    ClubCrestAssetId = c?.Details?.CrestAssetId,
                    MatchesPlayed = matchesPlayed,
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
                    WinPercent = matchesPlayed > 0 ? (wins * 100.0) / matchesPlayed : 0,
                    PassAccuracyPercent = passAttempts > 0 ? (passesMade * 100.0) / passAttempts : 0,
                    GoalAccuracyPercent = shots > 0 ? (goals * 100.0) / shots : 0
                };
            })
            .OrderByDescending(c => c.MatchesPlayed)
            .ToList();

        return Ok(new FullMatchStatisticsDto { Overall = overall, Players = playersStats, Clubs = clubsStats });
    }

    [HttpGet("matches/results")]
    public async Task<IActionResult> GetMatchResults(
        [FromQuery] long clubId,
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

        var q = _db.Matches
            .AsNoTracking()
            .Where(m => m.Clubs.Any(c => c.ClubId == clubId));

        if (matchType == MatchType.League) q = q.Where(m => m.MatchType == MatchType.League);
        else if (matchType == MatchType.Playoff) q = q.Where(m => m.MatchType == MatchType.Playoff);

        // Filtro opcional por quantidade de jogadores do adversário
        if (opponentCount.HasValue)
        {
            int oc = opponentCount.Value;
            q = q.Where(m =>
                m.MatchPlayers
                 .Where(mp => mp.Player.ClubId != clubId)
                 .Select(mp => mp.PlayerEntityId)
                 .Distinct()
                 .Count() == oc);
        }

        var matches = await q
            .Include(m => m.Clubs).ThenInclude(c => c.Details)
            .Include(m => m.MatchPlayers)
            .OrderByDescending(m => m.Timestamp)
            .ToListAsync(ct);

        var resultList = new List<MatchResultDto>(matches.Count);

        foreach (var match in matches)
        {
            var clubs = match.Clubs.OrderBy(c => c.Team).ToList();
            if (clubs.Count != 2) continue;

            var clubA = clubs[0];
            var clubB = clubs[1];

            short SumRedCards(long cid) =>
                (short)((match.MatchPlayers?
                    .Where(p => p.ClubId == cid)
                    .Sum(p => (int?)p.Redcards) ?? 0));

            var redA = SumRedCards(clubA.ClubId);
            var redB = SumRedCards(clubB.ClubId);

            var clubAPlayerCount = match.MatchPlayers
                .Where(mp => mp.ClubId == clubA.ClubId)
                .Select(mp => mp.PlayerEntityId)
                .Distinct()
                .Count();

            var clubBPlayerCount = match.MatchPlayers
                .Where(mp => mp.ClubId == clubB.ClubId)
                .Select(mp => mp.PlayerEntityId)
                .Distinct()
                .Count();

            var dto = new MatchResultDto
            {
                MatchId = match.MatchId,
                Timestamp = match.Timestamp,

                ClubAName = clubA.Details?.Name ?? $"Clube {clubA.ClubId}",
                ClubAGoals = clubA.Goals,
                ClubARedCards = redA,
                ClubAPlayerCount = clubAPlayerCount,
                ClubADetails = clubA.Details == null ? null : new ClubDetailsDto
                {
                    Name = clubA.Details.Name,
                    ClubId = clubA.ClubId,
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
                    CrestAssetId = clubA.Details.CrestAssetId,
                    SelectedKitType = clubA.Details.SelectedKitType
                },

                ClubBName = clubB.Details?.Name ?? $"Clube {clubB.ClubId}",
                ClubBGoals = clubB.Goals,
                ClubBRedCards = redB,
                ClubBPlayerCount = clubBPlayerCount,
                ClubBDetails = clubB.Details == null ? null : new ClubDetailsDto
                {
                    Name = clubB.Details.Name,
                    ClubId = clubB.ClubId,
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
                    CrestAssetId = clubB.Details.CrestAssetId,
                    SelectedKitType = clubB.Details.SelectedKitType
                },

                ResultText = $"{clubA.Details?.Name ?? "Clube A"} {clubA.Goals} x {clubB.Goals} {clubB.Details?.Name ?? "Clube B"}"
            };

            resultList.Add(dto);
        }

        return Ok(resultList);
    }

    [HttpGet("{playerId:long}")]
    public async Task<IActionResult> GetPlayerById(long playerId, CancellationToken ct)
    {
        var player = await _db.Players.AsNoTracking().FirstOrDefaultAsync(p => p.PlayerId == playerId, ct);
        return player is null ? NotFound() : Ok(player);
    }

    [HttpDelete("{matchId:long}")]
    public async Task<IActionResult> DeleteMatch(long matchId, CancellationToken ct)
    {
        var match = await _db.Matches
            .Include(m => m.MatchPlayers)
            .Include(m => m.Clubs)
            .FirstOrDefaultAsync(m => m.MatchId == matchId, ct);

        if (match == null)
            return NotFound(new { message = "Partida não encontrada" });

        var matchPlayers = await _db.MatchPlayers
            .Where(mp => mp.MatchId == matchId)
            .ToListAsync(ct);

        var statsIds = matchPlayers.Select(mp => mp.PlayerMatchStatsEntityId).ToList();
        var playerMatchStats = await _db.PlayerMatchStats
            .Where(pms => statsIds.Contains(pms.Id))
            .ToListAsync(ct);

        _db.PlayerMatchStats.RemoveRange(playerMatchStats);
        _db.MatchPlayers.RemoveRange(matchPlayers);

        var matchClubs = await _db.MatchClubs.Where(mc => mc.MatchId == matchId).ToListAsync(ct);
        _db.MatchClubs.RemoveRange(matchClubs);

        _db.Matches.Remove(match);
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }

    [HttpDelete("clubs/{clubId:long}/matches")]
    public async Task<IActionResult> DeleteMatchesByClub(long clubId, CancellationToken ct)
    {
        var clubExists = await _db.MatchClubs.AnyAsync(c => c.ClubId == clubId, ct);
        if (!clubExists)
            return NotFound(new { message = "Clube não encontrado" });

        var matchIds = await _db.MatchClubs
            .Where(mc => mc.ClubId == clubId)
            .Select(mc => mc.MatchId)
            .Distinct()
            .ToListAsync(ct);

        if (matchIds.Count == 0)
            return NoContent();

        // >>> TUDO dentro da execution strategy <<<
        var strategy = _db.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                // Coleta ids de stats
                var statsIds = await _db.MatchPlayers
                    .Where(mp => matchIds.Contains(mp.MatchId))
                    .Select(mp => mp.PlayerMatchStatsEntityId)
                    .Where(id => id != null)
                    .Cast<long>()
                    .Distinct()
                    .ToListAsync(ct);

                // Deleções (EF Core 7+)
                await _db.PlayerMatchStats
                    .Where(pms => statsIds.Contains(pms.Id))
                    .ExecuteDeleteAsync(ct);

                await _db.MatchPlayers
                    .Where(mp => matchIds.Contains(mp.MatchId))
                    .ExecuteDeleteAsync(ct);

                await _db.MatchClubs
                    .Where(mc => matchIds.Contains(mc.MatchId))
                    .ExecuteDeleteAsync(ct);

                await _db.Matches
                    .Where(m => matchIds.Contains(m.MatchId))
                    .ExecuteDeleteAsync(ct);

                // ExecuteDeleteAsync aplica direto no DB; SaveChanges é dispensável aqui,
                // mas não faz mal manter se você misturar com RemoveRange.
                // await _db.SaveChangesAsync(ct);

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

    [HttpGet("statistics/player/{matchId:long}/{playerId:long}")]
    public async Task<IActionResult> GetPlayerStatisticsByMatchAndPlayer(
    long matchId,
    long playerId,
    CancellationToken ct)
    {
        var dto = await _db.MatchPlayers
            .AsNoTracking()
            .Where(mp => mp.MatchId == matchId && mp.PlayerEntityId == playerId)
            .Select(Proj.MatchPlayerStatsRow)   // projeção server-side
            .FirstOrDefaultAsync(ct);

        if (dto == null)
            return NotFound($"Player with id {playerId} not found in match {matchId}.");

        return Ok(dto);
    }

}
