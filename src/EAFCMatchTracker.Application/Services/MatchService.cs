using EAFCMatchTracker.Application.Dtos;
using EAFCMatchTracker.Application.Extensions;
using EAFCMatchTracker.Application.Interfaces.Repositories;
using EAFCMatchTracker.Application.Interfaces.Services;
using EAFCMatchTracker.Domain.Entities;
using EAFCMatchTracker.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DomainMatchType = EAFCMatchTracker.Domain.Entities.MatchType;

namespace EAFCMatchTracker.Application.Services;

public class MatchService : IMatchService
{
    private const int MinOpponentPlayers = 2;
    private const int MaxOpponentPlayers = 11;

    private readonly IMatchRepository _matchRepository;
    private readonly IPlayerRepository _playerRepository;
    private readonly EAFCContext _db;
    private readonly ILogger<MatchService> _logger;

    public MatchService(
        IMatchRepository matchRepository,
        IPlayerRepository playerRepository,
        EAFCContext db,
        ILogger<MatchService> logger)
    {
        _matchRepository = matchRepository;
        _playerRepository = playerRepository;
        _db = db;
        _logger = logger;
    }

    public async Task<FullMatchStatisticsDto> GetMatchStatisticsAsync(long clubId, CancellationToken ct)
    {
        _logger.LogInformation("MatchService.GetMatchStatisticsAsync clubId={ClubId}", clubId);

        var matches = await _matchRepository.GetMatchesForClubFullAsync(clubId, ct);

        var allPlayers = matches
            .SelectMany(m => m.MatchPlayers)
            .Where(e => e.Player.ClubId == clubId)
            .ToList();

        if (allPlayers.Count == 0)
            return new FullMatchStatisticsDto();

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

        return new FullMatchStatisticsDto { Overall = overall, Players = playersStats, Clubs = clubsStats };
    }

    public async Task<FullMatchStatisticsDto> GetMatchStatisticsLimitedAsync(long clubId, int count, int? opponentCount, CancellationToken ct)
    {
        _logger.LogInformation("MatchService.GetMatchStatisticsLimitedAsync clubId={ClubId} count={Count}", clubId, count);

        var query = _matchRepository.QueryMatchesByClubId(clubId);
        query = ApplyOpponentFilter(query, clubId, opponentCount);

        var matches = await _matchRepository.GetPagedMatchesAsync(
            query.OrderByDescending(m => m.Timestamp), 0, count, ct);

        if (matches.Count == 0) return new FullMatchStatisticsDto();

        var (overall, players, clubs) = StatsAggregator.BuildLimitedForClub(clubId, matches);
        if (players.Count == 0) return new FullMatchStatisticsDto();

        return new FullMatchStatisticsDto { Overall = overall, Players = players, Clubs = clubs };
    }

    public async Task<List<FullMatchStatisticsByDayDto>> GetMatchStatisticsByDateRangeGroupedAsync(
        List<long> ids, DateTime startUtc, DateTime endExclusiveUtc, int? opponentCount, CancellationToken ct)
    {
        _logger.LogInformation("MatchService.GetMatchStatisticsByDateRangeGroupedAsync ids={Ids}", string.Join(",", ids));

        bool applyOpponentFilter = opponentCount.HasValue && ids.Count == 1;

        var q = _db.Matches
            .AsNoTracking()
            .Include(m => m.Clubs).ThenInclude(c => c.Details)
            .Include(m => m.MatchPlayers).ThenInclude(mp => mp.Player)
            .Where(m => m.Clubs.Any(c => ids.Contains(c.ClubId)))
            .Where(m => m.Timestamp >= startUtc && m.Timestamp < endExclusiveUtc);

        if (applyOpponentFilter)
            q = ApplyOpponentFilter(q, ids[0], opponentCount);

        var matches = await q.OrderBy(m => m.Timestamp).ToListAsync(ct);
        if (matches.Count == 0) return new List<FullMatchStatisticsByDayDto>();

        return matches
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
                    TotalMatches = countedMatches,
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
    }

    public async Task<List<PlayerStatisticsByDayDto>> GetPlayerMatchStatisticsByDateRangeGroupedAsync(
        long playerId, List<long> ids, DateTime startUtc, DateTime endExclusiveUtc, CancellationToken ct)
    {
        _logger.LogInformation("MatchService.GetPlayerMatchStatisticsByDateRangeGroupedAsync playerId={PlayerId}", playerId);

        var matches = await _matchRepository.GetMatchesForPlayerInClubsInDateRangeAsync(playerId, ids, startUtc, endExclusiveUtc, ct);
        if (matches.Count == 0) return new List<PlayerStatisticsByDayDto>();

        return matches
            .GroupBy(m => m.Timestamp.Date)
            .Select(g =>
            {
                var dayMatches = g.ToList();
                var statsPerMatch = new List<PlayerStatisticsDto>();

                foreach (var match in dayMatches)
                {
                    var playerMatchPlayers = match.MatchPlayers
                        .Where(mp => mp.Player.PlayerId == playerId && ids.Contains(mp.ClubId))
                        .ToList();

                    if (!playerMatchPlayers.Any()) continue;

                    var statsList = StatsAggregator.BuildPerPlayerMergedByGlobalId(playerMatchPlayers);
                    var stat = statsList.FirstOrDefault();
                    if (stat != null) statsPerMatch.Add(stat);
                }

                return new PlayerStatisticsByDayDto
                {
                    Date = DateOnly.FromDateTime(g.Key),
                    Statistics = statsPerMatch
                };
            })
            .OrderBy(x => x.Date)
            .ToList();
    }

    public async Task<PagedResult<MatchResultDto>> GetMatchResultsAsync(
        long clubId, DomainMatchType matchType, int? opponentCount, int page, int pageSize, CancellationToken ct)
    {
        _logger.LogInformation("MatchService.GetMatchResultsAsync clubId={ClubId}", clubId);

        IQueryable<MatchEntity> q = _db.Matches.AsNoTracking()
            .Where(m => m.Clubs.Any(c => c.ClubId == clubId));

        if (matchType == DomainMatchType.League) q = q.Where(m => m.MatchType == DomainMatchType.League);
        else if (matchType == DomainMatchType.Playoff) q = q.Where(m => m.MatchType == DomainMatchType.Playoff);

        q = ApplyOpponentFilter(q, clubId, opponentCount);

        var totalCount = await q.CountAsync(ct);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        var skip = (page - 1) * pageSize;

        var matches = await _matchRepository.GetPagedMatchesAsync(q, skip, pageSize, ct);

        var fallbackDivByClub = await _playerRepository.GetFallbackDivisionsAsync(
            matches.SelectMany(m => m.Clubs).Where(c => c.CurrentDivision == null).Select(c => c.ClubId).Distinct().ToList(), ct);

        var items = BuildMatchResultDtos(matches, fallbackDivByClub);

        return new PagedResult<MatchResultDto>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            HasPrevious = page > 1 && totalPages > 0,
            HasNext = page < totalPages,
            Items = items
        };
    }

    public async Task<PagedResult<MatchResultDto>> GetMultiClubMatchResultsAsync(
        List<long> ids, DomainMatchType matchType, int? opponentCount, int page, int pageSize, CancellationToken ct)
    {
        _logger.LogInformation("MatchService.GetMultiClubMatchResultsAsync ids={Ids}", string.Join(",", ids));

        IQueryable<MatchEntity> q = _db.Matches.AsNoTracking()
            .Where(m => m.Clubs.Any(c => ids.Contains(c.ClubId)));

        if (matchType == DomainMatchType.League) q = q.Where(m => m.MatchType == DomainMatchType.League);
        else if (matchType == DomainMatchType.Playoff) q = q.Where(m => m.MatchType == DomainMatchType.Playoff);

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

        var matches = await _matchRepository.GetPagedMatchesAsync(q, skip, pageSize, ct);

        var fallbackDivByClub = await _playerRepository.GetFallbackDivisionsAsync(
            matches.SelectMany(m => m.Clubs).Where(c => c.CurrentDivision == null).Select(c => c.ClubId).Distinct().ToList(), ct);

        var items = BuildMatchResultDtos(matches, fallbackDivByClub);

        return new PagedResult<MatchResultDto>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            HasPrevious = page > 1 && totalPages > 0,
            HasNext = page < totalPages,
            Items = items
        };
    }

    public async Task DeleteMatchesByClubAsync(long clubId, CancellationToken ct)
    {
        _logger.LogInformation("MatchService.DeleteMatchesByClubAsync clubId={ClubId}", clubId);
        var matchIds = await _matchRepository.GetMatchIdsByClubIdAsync(clubId, ct);
        if (matchIds.Count == 0) return;
        await _matchRepository.DeleteMatchesAsync(matchIds, ct);
    }

    public async Task<object> GetGroupedLimitedAsync(List<long> ids, int count, int? opponentCount, CancellationToken ct)
    {
        _logger.LogInformation("MatchService.GetGroupedLimitedAsync ids={Ids}", string.Join(",", ids));

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
            return new { players = Array.Empty<object>(), clubs = Array.Empty<object>() };

        var lastMatchIds = lastMatches.Select(x => x.MatchId).ToList();

        var players = await _db.MatchPlayers
            .AsNoTracking()
            .Where(p => lastMatchIds.Contains(p.MatchId) && ids.Contains(p.ClubId))
            .Include(p => p.Player)
            .Include(p => p.Match)
            .ToListAsync(ct);

        var playerStats = StatsAggregator.BuildPerPlayerMergedByGlobalId(players);
        var clubStatsSingle = StatsAggregator.BuildSingleClubFromPlayers(players, clubName: "Clubes agrupados");

        return new
        {
            players = playerStats,
            clubs = new[] { clubStatsSingle }
        };
    }

    public async Task<ClubRecordsDto> GetClubRecordsAsync(List<long> ids, CancellationToken ct)
    {
        _logger.LogInformation("MatchService.GetClubRecordsAsync ids={Ids}", string.Join(",", ids));

        var matches = await _matchRepository.GetMatchesByClubIdsAsync(ids, ct);

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
            bool isCleanSheet = ga == 0;
            bool isScoring = gf > 0;

            if (isWin) dto.TotalWins++;
            else if (isDraw) dto.TotalDraws++;
            else dto.TotalLosses++;

            if (isWin)
            {
                winStreak++; unbeatenStreak++;
                int diff = gf - ga;
                if (diff > bestWinDiff)
                {
                    bestWinDiff = diff;
                    biggestWin = new RecordMatchDto { MatchId = match.MatchId, Timestamp = match.Timestamp, GoalsFor = gf, GoalsAgainst = ga, OpponentName = oppClub?.Details?.Name };
                }
            }
            else if (isDraw) { winStreak = 0; unbeatenStreak++; }
            else
            {
                winStreak = 0; unbeatenStreak = 0;
                int diff = ga - gf;
                if (diff > bestLossDiff)
                {
                    bestLossDiff = diff;
                    biggestLoss = new RecordMatchDto { MatchId = match.MatchId, Timestamp = match.Timestamp, GoalsFor = gf, GoalsAgainst = ga, OpponentName = oppClub?.Details?.Name };
                }
            }

            if (isCleanSheet) cleanSheetStreak++; else cleanSheetStreak = 0;
            if (isScoring) scoringStreak++; else scoringStreak = 0;

            if (winStreak > maxWinStreak) maxWinStreak = winStreak;
            if (unbeatenStreak > maxUnbeatenStreak) maxUnbeatenStreak = unbeatenStreak;
            if (cleanSheetStreak > maxCleanSheetStreak) maxCleanSheetStreak = cleanSheetStreak;
            if (scoringStreak > maxScoringStreak) maxScoringStreak = scoringStreak;

            int total = gf + ga;
            if (total > bestTotal)
            {
                bestTotal = total;
                highestScoring = new RecordMatchDto { MatchId = match.MatchId, Timestamp = match.Timestamp, GoalsFor = gf, GoalsAgainst = ga, OpponentName = oppClub?.Details?.Name };
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
            .Select(x => new HatTrickDto { MatchId = x.MatchId, Timestamp = x.Timestamp, PlayerName = x.Name, Goals = x.Goals })
            .ToList();

        return dto;
    }

    public async Task<OpponentsAnalysisDto> GetOpponentsAnalysisAsync(List<long> ids, CancellationToken ct)
    {
        _logger.LogInformation("MatchService.GetOpponentsAnalysisAsync ids={Ids}", string.Join(",", ids));

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
                rec = new OpponentRecordDto { ClubId = oppClub.ClubId, Name = oppClub.Details?.Name ?? $"Clube {oppClub.ClubId}" };
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
            else rec.Draws++;
        }

        foreach (var rec in opponentMap.Values)
            rec.WinRate = rec.Matches > 0 ? Math.Round(rec.Wins * 100.0 / rec.Matches, 1) : 0;

        var opponents = opponentMap.Values.OrderByDescending(o => o.Matches).ToList();

        return new OpponentsAnalysisDto
        {
            TotalMatches = totalProcessed,
            TotalOpponents = opponents.Count,
            Opponents = opponents
        };
    }

    public async Task<PagedResult<MatchDto>> GetAllMatchesAsync(int page, int pageSize, CancellationToken ct)
    {
        _logger.LogInformation("MatchService.GetAllMatchesAsync page={Page}, pageSize={PageSize}", page, pageSize);

        var q = _db.Matches.AsNoTracking().OrderByDescending(m => m.Timestamp);
        var total = await q.CountAsync(ct);
        var items = await q.Skip((page - 1) * pageSize).Take(pageSize).ToMatchDtoListAsync(ct);

        return new PagedResult<MatchDto>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            TotalPages = (int)Math.Ceiling(total / (double)pageSize),
            HasPrevious = page > 1,
            HasNext = page * pageSize < total,
            Items = items
        };
    }

    public async Task<MatchDto?> GetMatchByIdAsync(long matchId, CancellationToken ct)
    {
        _logger.LogInformation("MatchService.GetMatchByIdAsync matchId={MatchId}", matchId);

        return await _db.Matches
            .AsNoTracking()
            .Where(m => m.MatchId == matchId)
            .FirstMatchDtoOrDefaultAsync(ct);
    }

    public async Task<MatchStatisticsResponseDto?> GetMatchStatisticsByIdAsync(long matchId, CancellationToken ct)
    {
        _logger.LogInformation("MatchService.GetMatchStatisticsByIdAsync matchId={MatchId}", matchId);

        var match = await _db.Matches
            .AsNoTracking()
            .Include(m => m.Clubs).ThenInclude(c => c.Details)
            .Include(m => m.MatchPlayers).ThenInclude(mp => mp.Player)
            .FirstOrDefaultAsync(m => m.MatchId == matchId, ct);

        if (match == null) return null;

        var overall = StatsAggregator.BuildOverallForSingleMatch(match.MatchPlayers);
        var playersStats = StatsAggregator.BuildPerPlayer(match.MatchPlayers, includeDisconnected: true);
        var clubsStats = StatsAggregator.BuildPerClub(match.MatchPlayers, match.Clubs.ToDictionary(c => c.ClubId));

        return new MatchStatisticsResponseDto
        {
            Overall = overall,
            Players = playersStats,
            Clubs = clubsStats
        };
    }

    public async Task<MatchEventAggregatesResponseDto?> GetMatchEventAggregatesAsync(long matchId, CancellationToken ct)
    {
        _logger.LogInformation("MatchService.GetMatchEventAggregatesAsync matchId={MatchId}", matchId);

        var match = await _db.Matches
            .AsNoTracking()
            .Include(m => m.Clubs).ThenInclude(c => c.Details)
            .Include(m => m.MatchPlayers).ThenInclude(mp => mp.Player)
            .FirstOrDefaultAsync(m => m.MatchId == matchId, ct);

        if (match == null) return null;

        return new MatchEventAggregatesResponseDto
        {
            Categories       = MatchEventDefinitions.Categories.ToList(),
            EventDefinitions = MatchEventDefinitions.All.ToList(),
            Clubs            = MatchAggregateParser.BuildClubAggregates(match.Clubs, match.MatchPlayers)
        };
    }

    public async Task<MatchPlayerStatsDto?> GetPlayerStatisticsByMatchAndPlayerAsync(long matchId, long playerId, CancellationToken ct)
    {
        _logger.LogInformation("MatchService.GetPlayerStatisticsByMatchAndPlayerAsync matchId={MatchId}, playerId={PlayerId}", matchId, playerId);

        return await _db.MatchPlayers
            .AsNoTracking()
            .Where(mp => mp.MatchId == matchId && mp.PlayerEntityId == playerId)
            .ProjectPlayerStats()
            .FirstOrDefaultAsync(ct);
    }

    public async Task DeleteMatchAsync(long matchId, CancellationToken ct)
    {
        _logger.LogInformation("MatchService.DeleteMatchAsync matchId={MatchId}", matchId);

        var match = await _db.Matches
            .Include(m => m.MatchPlayers)
            .Include(m => m.Clubs)
            .FirstOrDefaultAsync(m => m.MatchId == matchId, ct);

        if (match == null)
            throw new KeyNotFoundException($"Match {matchId} not found.");

        var matchPlayers = await _db.MatchPlayers.Where(mp => mp.MatchId == matchId).ToListAsync(ct);
        var statsIds = matchPlayers.Select(mp => mp.PlayerMatchStatsEntityId).ToList();
        var playerMatchStats = await _db.PlayerMatchStats.Where(pms => statsIds.Contains(pms.Id)).ToListAsync(ct);

        _db.PlayerMatchStats.RemoveRange(playerMatchStats);
        _db.MatchPlayers.RemoveRange(matchPlayers);

        var matchClubs = await _db.MatchClubs.Where(mc => mc.MatchId == matchId).ToListAsync(ct);
        _db.MatchClubs.RemoveRange(matchClubs);

        _db.Matches.Remove(match);
        await _db.SaveChangesAsync(ct);
    }

    // ─── Helpers ───────────────────────────────────────────────────────────────

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

    private static (int gf, int ga) ComputeGoalsForAgainst(IEnumerable<MatchEntity> matches, IReadOnlyCollection<long> ids)
    {
        int gf = 0, ga = 0;
        foreach (var m in matches)
        {
            gf += m.Clubs.Where(c => ids.Contains(c.ClubId)).Sum(c => c.Goals);
            ga += m.Clubs.Where(c => !ids.Contains(c.ClubId)).Sum(c => c.Goals);
        }
        return (gf, ga);
    }

    private static (int wins, int draws, int losses, int counted) ComputeWinsDrawsLosses(IEnumerable<MatchEntity> matches, IReadOnlyCollection<long> ids)
    {
        int wins = 0, draws = 0, losses = 0, counted = 0;
        foreach (var m in matches)
        {
            int ourTeams = m.Clubs.Count(c => ids.Contains(c.ClubId));
            if (ourTeams != 1) continue;

            int ourGoals = m.Clubs.Where(c => ids.Contains(c.ClubId)).Sum(c => c.Goals);
            int oppGoals = m.Clubs.Where(c => !ids.Contains(c.ClubId)).Sum(c => c.Goals);

            if (ourGoals > oppGoals) wins++;
            else if (ourGoals < oppGoals) losses++;
            else draws++;

            counted++;
        }
        return (wins, draws, losses, counted);
    }

    private static List<MatchResultDto> BuildMatchResultDtos(
        IEnumerable<MatchEntity> matches,
        Dictionary<long, int?> fallbackDivByClub)
    {
        var items = new List<MatchResultDto>();
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
        return items;
    }

    private static long? GetManOfTheMatchId(MatchEntity match)
    {
        var flagged = match.MatchPlayers
            .Where(mp => mp.Mom)
            .Select(mp => new { mp.PlayerEntityId, mp.Rating, Score = mp.Goals + mp.Assists })
            .ToList();

        if (flagged.Count == 1) return flagged[0].PlayerEntityId;
        if (flagged.Count > 1)
            return flagged.OrderByDescending(x => x.Rating).ThenByDescending(x => x.Score).ThenBy(x => x.PlayerEntityId).First().PlayerEntityId;

        var best = match.MatchPlayers
            .Select(mp => new { mp.PlayerEntityId, mp.Rating, Score = mp.Goals + mp.Assists })
            .OrderByDescending(x => x.Rating).ThenByDescending(x => x.Score).ThenBy(x => x.PlayerEntityId)
            .FirstOrDefault();

        return best?.PlayerEntityId;
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
            .Select(g => new { Goals = g.Sum(x => (int)x.Goals), Name = g.Select(x => x.Player?.Playername).FirstOrDefault() })
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
            .OrderByDescending(x => x.CleanSheetsGk).ThenByDescending(x => x.Saves).ThenByDescending(x => x.Rating).ThenBy(x => x.Name)
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
}
