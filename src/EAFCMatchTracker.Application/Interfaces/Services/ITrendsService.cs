namespace EAFCMatchTracker.Application.Interfaces.Services;

public interface ITrendsService
{
    Task<object> GetClubTrendsAsync(long clubId, int last, DateTime? since, DateTime? until, CancellationToken ct);
    Task<object> GetTopScorersAsync(long clubId, DateTime? since, DateTime? until, int limit, CancellationToken ct);
}
