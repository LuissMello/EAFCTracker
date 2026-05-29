using EAFCMatchTracker.Domain.Entities;

namespace EAFCMatchTracker.Application.Interfaces.Repositories;

public interface IClubRepository
{
    Task<List<(long ClubId, string? Name, int Team)>> GetClubSummariesByIdsAsync(List<long> ids, CancellationToken ct);
    Task<List<MatchClubEntity>> GetMatchClubsByIdsAsync(List<long> ids, CancellationToken ct);
    Task<bool> ExistsAsync(long clubId, CancellationToken ct);
    Task<OverallStatsEntity?> GetOverallStatsByClubIdAsync(long clubId, CancellationToken ct);
    Task<List<OverallStatsEntity>> GetAllOverallStatsByClubIdAsync(long clubId, CancellationToken ct);
    Task<List<OverallStatsEntity>> GetOverallStatsByClubIdsAsync(List<long> ids, CancellationToken ct);
    Task<List<PlayoffAchievementEntity>> GetPlayoffAchievementsByClubIdAsync(long clubId, CancellationToken ct);
    Task<List<long>> GetAllDistinctClubIdsAsync(CancellationToken ct);
    Task<string?> GetLatestClubNameAsync(long clubId, CancellationToken ct);
    Task<List<PlayoffAchievementEntity>> GetPlayoffAchievementsForUpdateAsync(long clubId, CancellationToken ct);
    Task AddOverallStatsAsync(OverallStatsEntity entity, CancellationToken ct);
    Task UpdateOverallStatsAsync(OverallStatsEntity entity);
    Task UpdateOverallStatsRangeAsync(IEnumerable<OverallStatsEntity> entities);
    Task AddPlayoffAchievementAsync(PlayoffAchievementEntity entity, CancellationToken ct);
    Task UpdatePlayoffAchievementAsync(PlayoffAchievementEntity entity);
    Task SaveChangesAsync(CancellationToken ct);
}
