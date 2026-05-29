using EAFCMatchTracker.Application.Dtos;

namespace EAFCMatchTracker.Application.Interfaces.Services;

public interface IClubService
{
    Task<List<ClubListItemDto>> GetAllAsync(CancellationToken ct);
    Task<List<ClubOverallStatsDto>> GetOverallAsync(long clubId, CancellationToken ct);
    Task<List<ClubPlayoffAchievementDto>> GetPlayoffsAsync(long clubId, CancellationToken ct);
}
