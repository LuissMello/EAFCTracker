using EAFCMatchTracker.Application.Dtos;
using EAFCMatchTracker.Domain.Entities;

namespace EAFCMatchTracker.Application.Interfaces.Services;

public interface IPlayerService
{
    Task<PlayerEntity?> GetByIdAsync(long playerId, CancellationToken ct);
    Task<PlayerProfileDto> GetProfileAsync(long playerEntityId, CancellationToken ct);
    Task<List<PlayerAttributeSnapshotDto>> GetClubPlayersAttributesAsync(long clubId, int count, CancellationToken ct);
    Task<List<PlayerStatisticsDto>> GetClubPlayersAggregateAsync(long clubId, int count, int? opponentCount, CancellationToken ct);
}
