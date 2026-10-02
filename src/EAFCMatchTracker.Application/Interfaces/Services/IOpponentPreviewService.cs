using EAFCMatchTracker.Application.Dtos;

namespace EAFCMatchTracker.Application.Interfaces.Services;

public interface IOpponentPreviewService
{
    /// <summary>
    /// Pré-estatísticas do adversário (EA: busca por nome/overall/últimas partidas/membros; nosso banco: confronto
    /// direto). Cada bloco é opcional; lacunas são explicadas em <c>Warnings</c>. Resultado da EA em cache ~5 min.
    /// </summary>
    Task<OpponentPreviewDto> GetPreviewAsync(long opponentClubId, long? clubId, string? name, CancellationToken ct);
}
