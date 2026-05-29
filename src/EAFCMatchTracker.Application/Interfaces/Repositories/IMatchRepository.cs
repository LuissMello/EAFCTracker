using EAFCMatchTracker.Domain.Entities;

namespace EAFCMatchTracker.Application.Interfaces.Repositories;

public interface IMatchRepository
{
    IQueryable<MatchEntity> QueryMatchesByClubId(long clubId);
    IQueryable<MatchEntity> QueryMatchesByClubIds(IReadOnlyCollection<long> ids);
    Task<List<MatchEntity>> GetMatchesForClubWithPlayersAsync(long clubId, int count, CancellationToken ct);
    Task<List<MatchEntity>> GetMatchesForClubFullAsync(long clubId, CancellationToken ct);
    Task<List<MatchEntity>> GetMatchesForClubIdsInDateRangeAsync(IReadOnlyCollection<long> ids, DateTime startUtc, DateTime endExclusiveUtc, CancellationToken ct);
    Task<List<MatchEntity>> GetMatchesForPlayerInClubsInDateRangeAsync(long playerId, IReadOnlyCollection<long> clubIds, DateTime startUtc, DateTime endExclusiveUtc, CancellationToken ct);
    Task<int> CountMatchesByQueryAsync(IQueryable<MatchEntity> query, CancellationToken ct);
    Task<List<MatchEntity>> GetPagedMatchesAsync(IQueryable<MatchEntity> query, int skip, int take, CancellationToken ct);
    Task<List<MatchEntity>> GetMatchesByClubIdsAsync(IReadOnlyCollection<long> ids, CancellationToken ct);
    Task<MatchEntity?> GetMatchWithPlayersAndClubsAsync(long matchId, CancellationToken ct);
    Task<List<long>> GetMatchIdsByClubIdAsync(long clubId, CancellationToken ct);
    Task DeleteMatchesAsync(IEnumerable<long> matchIds, CancellationToken ct);
    Task<List<MatchEntity>> GetRecentMatchesWithFullDataAsync(int count, CancellationToken ct);
    Task<List<MatchEntity>> GetMatchesForTrendsAsync(long clubId, int last, DateTime? since, DateTime? until, CancellationToken ct);
}
