using EAFCMatchTracker.Application.Dtos;
using EAFCMatchTracker.Application.Interfaces.Repositories;
using EAFCMatchTracker.Application.Interfaces.Services;
using EAFCMatchTracker.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EAFCMatchTracker.Application.Services;

public class ClubService : IClubService
{
    private readonly IClubRepository _clubRepository;
    private readonly IConfiguration _config;
    private readonly ILogger<ClubService> _logger;
    private readonly EAFCContext _db;

    public ClubService(IClubRepository clubRepository, IConfiguration config, ILogger<ClubService> logger, EAFCContext db)
    {
        _clubRepository = clubRepository;
        _config = config;
        _logger = logger;
        _db = db;
    }

    public async Task<List<ClubListItemDto>> GetAllAsync(CancellationToken ct)
    {
        _logger.LogInformation("ClubService.GetAllAsync called");
        var ids = ParseClubIdsFromConfig(_config);
        if (ids.Count == 0) return new List<ClubListItemDto>();

        var summaries = await _clubRepository.GetClubSummariesByIdsAsync(ids, ct);

        return summaries.Select(c => new ClubListItemDto
        {
            ClubId = c.ClubId,
            Name = c.Name ?? $"Clube {c.ClubId}",
            CrestAssetId = c.Team.ToString()
        }).ToList();
    }

    public async Task<PagedResult<ClubOverallStatsDto>> GetOverallPagedAsync(long clubId, int page, int pageSize, CancellationToken ct)
    {
        _logger.LogInformation("ClubService.GetOverallPagedAsync clubId={ClubId} page={Page} pageSize={PageSize}", clubId, page, pageSize);

        var q = _db.OverallStats.AsNoTracking().Where(o => o.ClubId == clubId);
        var totalCount = await q.CountAsync(ct);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

        var entities = await q
            .OrderByDescending(o => o.UpdatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<ClubOverallStatsDto>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            HasPrevious = page > 1 && totalPages > 0,
            HasNext = page < totalPages,
            Items = StatsAggregator.BuildClubsOverall(entities)
        };
    }

    public async Task<PagedResult<MatchWithOverallStatsDto>> GetMatchesWithOverallAsync(long clubId, int page, int pageSize, CancellationToken ct)
    {
        _logger.LogInformation("ClubService.GetMatchesWithOverallAsync clubId={ClubId} page={Page} pageSize={PageSize}", clubId, page, pageSize);

        var matchIdsQ = _db.MatchClubs.AsNoTracking()
            .Where(mc => mc.ClubId == clubId)
            .OrderByDescending(mc => mc.Date)
            .Select(mc => mc.MatchId);

        var totalCount = await matchIdsQ.CountAsync(ct);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

        var pagedMatchIds = await matchIdsQ
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        if (pagedMatchIds.Count == 0)
            return new PagedResult<MatchWithOverallStatsDto>
            {
                Page = page, PageSize = pageSize, TotalCount = totalCount,
                TotalPages = totalPages, HasPrevious = page > 1, HasNext = false, Items = []
            };

        var allMatchClubs = await _db.MatchClubs.AsNoTracking()
            .Where(mc => pagedMatchIds.Contains(mc.MatchId))
            .ToListAsync(ct);

        var involvedClubIds = allMatchClubs.Select(mc => mc.ClubId).Distinct().ToList();

        // Exact records (new data): MatchId populated and in the paged set
        var exactOverall = await _db.OverallStats.AsNoTracking()
            .Where(os => os.MatchId != null && pagedMatchIds.Contains(os.MatchId.Value))
            .ToListAsync(ct);

        // Fallback records (legacy data): MatchId null, one per club
        var fallbackOverall = await _db.OverallStats.AsNoTracking()
            .Where(os => os.MatchId == null && involvedClubIds.Contains(os.ClubId))
            .ToListAsync(ct);

        var exactByMatchAndClub = exactOverall
            .GroupBy(os => (os.MatchId!.Value, os.ClubId))
            .ToDictionary(g => g.Key, g => g.First());

        var fallbackByClub = fallbackOverall
            .GroupBy(os => os.ClubId)
            .ToDictionary(g => g.Key, g => g.First());

        var items = new List<MatchWithOverallStatsDto>();

        foreach (var matchId in pagedMatchIds)
        {
            var clubs = allMatchClubs.Where(mc => mc.MatchId == matchId).ToList();
            var ours = clubs.FirstOrDefault(mc => mc.ClubId == clubId);
            var opp = clubs.FirstOrDefault(mc => mc.ClubId != clubId);
            if (ours == null || opp == null) continue;

            var ourOverall = ResolveOverall(matchId, clubId, exactByMatchAndClub, fallbackByClub);
            var oppOverall = opp != null ? ResolveOverall(matchId, opp.ClubId, exactByMatchAndClub, fallbackByClub) : null;

            items.Add(new MatchWithOverallStatsDto
            {
                MatchId = matchId,
                Date = ours.Date,
                OurClub = new MatchClubOverallDto
                {
                    ClubId = ours.ClubId,
                    ClubName = ours.Details?.Name,
                    Goals = ours.Goals,
                    Result = ours.Result,
                    OverallStats = ourOverall == null ? null : StatsAggregator.BuildClubsOverall([ourOverall]).FirstOrDefault()
                },
                Opponent = new MatchClubOverallDto
                {
                    ClubId = opp!.ClubId,
                    ClubName = opp.Details?.Name,
                    Goals = opp.Goals,
                    Result = opp.Result,
                    OverallStats = oppOverall == null ? null : StatsAggregator.BuildClubsOverall([oppOverall]).FirstOrDefault()
                }
            });
        }

        return new PagedResult<MatchWithOverallStatsDto>
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

    public async Task<List<ClubPlayoffAchievementDto>> GetPlayoffsAsync(long clubId, CancellationToken ct)
    {
        _logger.LogInformation("ClubService.GetPlayoffsAsync for clubId={ClubId}", clubId);
        var entities = await _clubRepository.GetPlayoffAchievementsByClubIdAsync(clubId, ct);
        return StatsAggregator.BuildClubsPlayoffAchievements(entities);
    }

    private static Domain.Entities.OverallStatsEntity? ResolveOverall(
        long matchId,
        long clubId,
        Dictionary<(long, long), Domain.Entities.OverallStatsEntity> exact,
        Dictionary<long, Domain.Entities.OverallStatsEntity> fallback)
    {
        if (exact.TryGetValue((matchId, clubId), out var e)) return e;
        fallback.TryGetValue(clubId, out var f);
        return f;
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
}
