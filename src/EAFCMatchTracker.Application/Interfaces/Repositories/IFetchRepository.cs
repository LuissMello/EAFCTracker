using EAFCMatchTracker.Domain.Entities;

namespace EAFCMatchTracker.Application.Interfaces.Repositories;

public interface IFetchRepository
{
    /// <summary>Retorna o registro singleton (Id=1) para exibição, sem tracking.</summary>
    Task<SystemFetchAudit?> GetAuditReadOnlyAsync(CancellationToken ct);

    /// <summary>
    /// Garante que o registro singleton (Id=1) existe e atualiza LastFetchedAt.
    /// Cria o registro caso ainda não exista.
    /// </summary>
    Task UpsertAuditAsync(DateTimeOffset lastFetchedAt, CancellationToken ct);
}
