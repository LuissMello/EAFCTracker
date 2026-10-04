using EAFCMatchTracker.Application.Dtos;

namespace EAFCMatchTracker.Application.Interfaces.Services;

public interface IPlayerCardService
{
    Task<PlayerCardsDto> GetCardsAsync(
        long clubId, DateOnly? from, DateOnly? to, int? gameVersion, int minMatches, CancellationToken ct);

    /// <summary>Compara dois jogadores do clube. Nulo quando algum deles não pertence ao clube (o controller responde 404).</summary>
    Task<PlayerCompareDto?> GetCompareAsync(
        long clubId, long a, long b, DateOnly? from, DateOnly? to, int? gameVersion, CancellationToken ct);
}
