using EAFCMatchTracker.Domain.Entities;

namespace EAFCMatchTracker.Application.Interfaces.Repositories;

public interface IGameVersionRepository
{
    Task<List<GameVersionEntity>> GetAllAsync(CancellationToken ct);
    Task<GameVersionEntity?> GetByVersionAsync(int version, CancellationToken ct);
    Task<GameVersionEntity?> GetCurrentAsync(CancellationToken ct);

    /// <summary>Cria uma edição. Retorna (null, false) se já existir uma com o mesmo número.</summary>
    Task<(GameVersionEntity? Entity, bool Created)> CreateAsync(int version, string name, DateTimeOffset? startsAt, CancellationToken ct);

    /// <summary>Torna a edição corrente (desmarca as demais) numa transação. Retorna null se não existir.</summary>
    Task<GameVersionEntity?> SetCurrentAsync(int version, CancellationToken ct);
}
