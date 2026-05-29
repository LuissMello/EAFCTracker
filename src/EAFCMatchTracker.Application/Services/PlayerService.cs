using EAFCMatchTracker.Application.Dtos;
using EAFCMatchTracker.Application.Interfaces.Repositories;
using EAFCMatchTracker.Application.Interfaces.Services;
using EAFCMatchTracker.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace EAFCMatchTracker.Application.Services;

public class PlayerService : IPlayerService
{
    private readonly IPlayerRepository _playerRepository;
    private readonly IMatchRepository _matchRepository;
    private readonly ILogger<PlayerService> _logger;

    public PlayerService(IPlayerRepository playerRepository, IMatchRepository matchRepository, ILogger<PlayerService> logger)
    {
        _playerRepository = playerRepository;
        _matchRepository = matchRepository;
        _logger = logger;
    }

    public async Task<PlayerEntity?> GetByIdAsync(long playerId, CancellationToken ct)
    {
        return await _playerRepository.GetByPlayerIdAsync(playerId, ct);
    }

    public async Task<PlayerProfileDto> GetProfileAsync(long playerEntityId, CancellationToken ct)
    {
        _logger.LogInformation("PlayerService.GetProfileAsync for playerEntityId={PlayerEntityId}", playerEntityId);

        var matchPlayers = await _playerRepository.GetMatchPlayersByEntityIdAsync(playerEntityId, ct);
        if (matchPlayers.Count == 0)
            throw new KeyNotFoundException($"Player {playerEntityId} not found.");

        var first = matchPlayers.First();
        var playerEntity = first.Player;

        var proName = matchPlayers
            .OrderByDescending(mp => mp.Match.Timestamp)
            .Select(mp => mp.ProName)
            .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));

        var name = !string.IsNullOrWhiteSpace(proName) ? proName : playerEntity?.Playername ?? "";
        var accountName = playerEntity?.Playername ?? "";

        var history = new List<PlayerMatchHistoryDto>();

        double bestRating = double.MinValue;
        double worstRating = double.MaxValue;
        long? bestRatingMatchId = null;
        long? worstRatingMatchId = null;

        foreach (var mp in matchPlayers)
        {
            var match = mp.Match;
            var ourClub = match.Clubs.FirstOrDefault(c => c.ClubId == mp.ClubId);
            var oppClub = match.Clubs.FirstOrDefault(c => c.ClubId != mp.ClubId);

            int gf = ourClub?.Goals ?? 0;
            int ga = oppClub?.Goals ?? 0;

            string result;
            if (gf > ga) result = "W";
            else if (gf < ga) result = "L";
            else result = "D";

            history.Add(new PlayerMatchHistoryDto
            {
                MatchId = match.MatchId,
                Timestamp = match.Timestamp,
                Goals = mp.Goals,
                Assists = mp.Assists,
                PreAssists = mp.PreAssists,
                Rating = mp.Rating,
                Pos = mp.Pos ?? "",
                Mom = mp.Mom,
                SecondsPlayed = mp.SecondsPlayed,
                Result = result,
                GoalsFor = gf,
                GoalsAgainst = ga,
                OpponentName = oppClub?.Details?.Name
            });

            if (mp.Rating > bestRating)
            {
                bestRating = mp.Rating;
                bestRatingMatchId = match.MatchId;
            }
            if (mp.Rating < worstRating)
            {
                worstRating = mp.Rating;
                worstRatingMatchId = match.MatchId;
            }
        }

        history = history.OrderByDescending(h => h.Timestamp).ToList();

        int totalWins = history.Count(h => h.Result == "W");
        int totalDraws = history.Count(h => h.Result == "D");
        int totalLosses = history.Count(h => h.Result == "L");

        var positions = matchPlayers
            .GroupBy(mp => mp.Pos ?? "")
            .ToDictionary(g => g.Key, g => g.Count());

        var proOverall = matchPlayers
            .OrderByDescending(mp => mp.Match.Timestamp)
            .Select(mp => mp.ProOverall)
            .FirstOrDefault(v => v.HasValue);

        return new PlayerProfileDto
        {
            PlayerEntityId = playerEntityId,
            Name = name,
            AccountName = accountName,
            PlayerId = playerEntity?.PlayerId ?? 0,
            ClubId = playerEntity?.ClubId ?? 0,
            TotalMatches = matchPlayers.Count,
            TotalWins = totalWins,
            TotalDraws = totalDraws,
            TotalLosses = totalLosses,
            TotalGoals = matchPlayers.Sum(mp => (int)mp.Goals),
            TotalAssists = matchPlayers.Sum(mp => (int)mp.Assists),
            TotalPreAssists = matchPlayers.Sum(mp => (int)mp.PreAssists),
            AvgRating = matchPlayers.Count > 0 ? matchPlayers.Average(mp => mp.Rating) : 0,
            TotalMoM = matchPlayers.Count(mp => mp.Mom),
            TotalRedCards = matchPlayers.Sum(mp => (int)mp.Redcards),
            TotalCleanSheets = matchPlayers.Sum(mp => (int)mp.Cleansheetsany),
            TotalSaves = matchPlayers.Sum(mp => (int)mp.Saves),
            HatTricks = matchPlayers.Count(mp => mp.Goals >= 3),
            BestRating = bestRating == double.MinValue ? 0 : bestRating,
            WorstRating = worstRating == double.MaxValue ? 0 : worstRating,
            BestRatingMatchId = bestRatingMatchId,
            WorstRatingMatchId = worstRatingMatchId,
            MostGoalsInMatch = matchPlayers.Max(mp => (int)mp.Goals),
            MostAssistsInMatch = matchPlayers.Max(mp => (int)mp.Assists),
            ProOverall = proOverall,
            Positions = positions,
            History = history
        };
    }

    public async Task<List<PlayerAttributeSnapshotDto>> GetClubPlayersAttributesAsync(long clubId, int count, CancellationToken ct)
    {
        _logger.LogInformation("PlayerService.GetClubPlayersAttributesAsync clubId={ClubId} count={Count}", clubId, count);

        var matches = await _matchRepository.GetMatchesForClubWithPlayersAsync(clubId, count, ct);
        if (matches.Count == 0) return new List<PlayerAttributeSnapshotDto>();

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

        return byPlayer;
    }

    public async Task<List<PlayerStatisticsDto>> GetClubPlayersAggregateAsync(long clubId, int count, int? opponentCount, CancellationToken ct)
    {
        _logger.LogInformation("PlayerService.GetClubPlayersAggregateAsync clubId={ClubId} count={Count} opponentCount={OpponentCount}", clubId, count, opponentCount);

        var query = _matchRepository.QueryMatchesByClubId(clubId);
        query = ApplyOpponentFilter(query, clubId, opponentCount);

        var matches = await _matchRepository.GetPagedMatchesAsync(
            query.OrderByDescending(m => m.Timestamp), 0, count, ct);

        if (matches.Count == 0)
            return new List<PlayerStatisticsDto>();

        var (_, players, _) = StatsAggregator.BuildLimitedForClub(clubId, matches);
        return players;
    }

    private static IQueryable<Domain.Entities.MatchEntity> ApplyOpponentFilter(
        IQueryable<Domain.Entities.MatchEntity> q, long clubId, int? opponentCount)
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
}
