using EAFCMatchTracker.Application.Dtos;

namespace EAFCMatchTracker.Application.Interfaces.Services;

public interface IGameNightService
{
    /// <summary>Lista leve de noites de jogo (sessões do clube), da mais antiga para a mais recente.</summary>
    Task<GameNightListDto> GetNightsAsync(long clubId, int? gameVersion, CancellationToken ct);

    /// <summary>Detalhe de uma noite; nulo se <paramref name="sessionId"/> não é uma sessão deste clube.</summary>
    Task<GameNightDetailDto?> GetNightAsync(long clubId, long sessionId, int? gameVersion, CancellationToken ct);
}
