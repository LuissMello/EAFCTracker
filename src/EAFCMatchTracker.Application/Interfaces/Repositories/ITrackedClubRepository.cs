using EAFCMatchTracker.Domain.Entities;

namespace EAFCMatchTracker.Application.Interfaces.Repositories;

public interface ITrackedClubRepository
{
    Task<List<TrackedClubEntity>> GetAllAsync(CancellationToken ct);
    Task<TrackedClubEntity?> GetByIdAsync(long clubId, CancellationToken ct);

    /// <summary>Adiciona o clube; se já existir, mantém o registro atual e retorna Created=false.</summary>
    Task<(TrackedClubEntity Club, bool Created)> AddAsync(TrackedClubEntity club, CancellationToken ct);

    /// <summary>Altera a edição do jogo do clube. Retorna null se o clube não estiver rastreado.</summary>
    Task<TrackedClubEntity?> SetGameVersionAsync(long clubId, int? gameVersionId, CancellationToken ct);

    Task<bool> RemoveAsync(long clubId, CancellationToken ct);
}
