using EAFCMatchTracker.Domain.Entities;

namespace EAFCMatchTracker.Application.Interfaces.Repositories;

public interface IPlayerRepository
{
    Task<PlayerEntity?> GetByPlayerIdAsync(long playerId, CancellationToken ct);
    Task<List<MatchPlayerEntity>> GetMatchPlayersForClubAsync(long clubId, CancellationToken ct);
    Task<List<MatchPlayerEntity>> GetMatchPlayersForClubInMatchesAsync(long clubId, List<long> matchIds, CancellationToken ct);
    Task<List<MatchPlayerEntity>> GetMatchPlayersByEntityIdAsync(long playerEntityId, CancellationToken ct);
    Task<Dictionary<long, int?>> GetFallbackDivisionsAsync(List<long> clubIds, CancellationToken ct);
    Task UpdateMatchPlayersRangeAsync(IEnumerable<MatchPlayerEntity> players);
    Task SaveChangesAsync(CancellationToken ct);
}
