using EAFCMatchTracker.Application.Dtos;

namespace EAFCMatchTracker.Application.Interfaces.Services;

public interface IOpponentSearchService
{
    /// <summary>
    /// Busca de adversários: histórico do clube (contém/tokens, sem acento/caixa) + busca por prefixo da EA,
    /// mesclados por clubId. Lança <see cref="Exceptions.DomainValidationException"/> se a consulta tiver &lt; 2 caracteres.
    /// </summary>
    Task<OpponentSearchResponseDto> SearchAsync(string query, long? clubId, int? limit, CancellationToken ct);
}
