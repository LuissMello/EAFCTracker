using EAFCMatchTracker.Application.Dtos;
using EAFCMatchTracker.Domain.Entities;

namespace EAFCMatchTracker.Application.Interfaces.Services;

public interface IPlayerService
{
    Task<PlayerDto?> GetByIdAsync(long playerId, CancellationToken ct);
    Task<PlayerProfileDto> GetProfileAsync(long playerEntityId, CancellationToken ct);
    /// <summary>Perfil com histórico/uso filtrados por arquétipo e/ou grupo da posição (os totais do perfil não mudam).</summary>
    Task<PlayerProfileDto> GetProfileAsync(long playerEntityId, int? archetypeId, string? positionGroup, CancellationToken ct);
    Task<List<PlayerAttributeSnapshotDto>> GetClubPlayersAttributesAsync(long clubId, int count, CancellationToken ct);
    Task<List<PlayerStatisticsDto>> GetClubPlayersAggregateAsync(long clubId, int count, int? opponentCount, CancellationToken ct);
}
