using EAFCMatchTracker.Application.Dtos;
using EAFCMatchTracker.Application.Interfaces.Repositories;
using EAFCMatchTracker.Application.Interfaces.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EAFCMatchTracker.Application.Services;

public class ClubService : IClubService
{
    private readonly IClubRepository _clubRepository;
    private readonly IConfiguration _config;
    private readonly ILogger<ClubService> _logger;

    public ClubService(IClubRepository clubRepository, IConfiguration config, ILogger<ClubService> logger)
    {
        _clubRepository = clubRepository;
        _config = config;
        _logger = logger;
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

    public async Task<List<ClubOverallStatsDto>> GetOverallAsync(long clubId, CancellationToken ct)
    {
        _logger.LogInformation("ClubService.GetOverallAsync for clubId={ClubId}", clubId);
        var entities = await _clubRepository.GetOverallStatsByClubIdsAsync(new List<long> { clubId }, ct);
        return StatsAggregator.BuildClubsOverall(entities);
    }

    public async Task<List<ClubPlayoffAchievementDto>> GetPlayoffsAsync(long clubId, CancellationToken ct)
    {
        _logger.LogInformation("ClubService.GetPlayoffsAsync for clubId={ClubId}", clubId);
        var entities = await _clubRepository.GetPlayoffAchievementsByClubIdAsync(clubId, ct);
        return StatsAggregator.BuildClubsPlayoffAchievements(entities);
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
