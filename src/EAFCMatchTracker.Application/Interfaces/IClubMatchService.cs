namespace EAFCMatchTracker.Application.Interfaces;

public interface IClubMatchService
{
    Task FetchAndStoreMatchesAsync(string clubId, string matchType, CancellationToken ct);
}
