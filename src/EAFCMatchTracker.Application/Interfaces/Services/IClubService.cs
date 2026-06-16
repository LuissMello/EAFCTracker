using EAFCMatchTracker.Application.Dtos;

namespace EAFCMatchTracker.Application.Interfaces.Services;

public interface IClubService
{
    Task<List<ClubListItemDto>> GetAllAsync(CancellationToken ct);
    Task<PagedResult<ClubOverallStatsDto>> GetOverallPagedAsync(long clubId, int page, int pageSize, CancellationToken ct);
    Task<PagedResult<MatchWithOverallStatsDto>> GetMatchesWithOverallAsync(long clubId, int page, int pageSize, CancellationToken ct);
    Task<ClubOverallStatsDto?> GetOverallForMatchAsync(long clubId, long matchId, CancellationToken ct);
    Task<List<ClubPlayoffAchievementDto>> GetPlayoffsAsync(long clubId, CancellationToken ct);
}
