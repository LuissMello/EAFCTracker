namespace EAFCMatchTracker.Application.Interfaces.Services;

public interface IMaintenanceService
{
    Task<object> RefreshClubsOverallAsync(CancellationToken ct);
    Task<object> RefreshClubCurrentDivisionAsync(long clubId, string? name, CancellationToken ct);
    Task<object> EnrichMatchPlayersWithMembersAsync(long clubId, CancellationToken ct);
    Task<object> RefreshOpponentsCurrentDivisionAsync(long clubId, CancellationToken ct);
    Task<object> RefreshAllPlayoffsAchievementsAsync(CancellationToken ct);
    Task<object> RefreshAllOverallStatsAsync(CancellationToken ct);
    Task<object> RefreshAllCurrentDivisionsAsync(CancellationToken ct);
    Task<object> EnrichAllMatchPlayersWithMembersAsync(CancellationToken ct);
    Task<object> RefreshEverythingAsync(CancellationToken ct);
    Task<object> GetRecentMatchesWithAggregatesAsync(int count, CancellationToken ct);
}
