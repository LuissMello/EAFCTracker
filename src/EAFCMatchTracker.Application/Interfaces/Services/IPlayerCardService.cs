using EAFCMatchTracker.Application.Dtos;

namespace EAFCMatchTracker.Application.Interfaces.Services;

public interface IPlayerCardService
{
    Task<PlayerCardsDto> GetCardsAsync(
        long clubId, DateOnly? from, DateOnly? to, int? gameVersion, int minMatches, CancellationToken ct);

    /// <summary>Cartas só com as partidas jogadas com <paramref name="archetypeId"/> (nulo = todas).</summary>
    Task<PlayerCardsDto> GetCardsAsync(
        long clubId, DateOnly? from, DateOnly? to, int? gameVersion, int minMatches, int? archetypeId, CancellationToken ct);

    Task<PlayerCompareDto?> GetCompareAsync(
        long clubId, long a, long b, DateOnly? from, DateOnly? to, int? gameVersion, int? archetypeId, CancellationToken ct);

    /// <summary>Filtros opcionais de arquétipo e de grupo da posição (ATAQUE/MEIO/DEFESA/GOLEIRO), combinados com AND.</summary>
    Task<PlayerCardsDto> GetCardsAsync(
        long clubId, DateOnly? from, DateOnly? to, int? gameVersion, int minMatches, int? archetypeId, string? positionGroup,
        CancellationToken ct);

    Task<PlayerCompareDto?> GetCompareAsync(
        long clubId, long a, long b, DateOnly? from, DateOnly? to, int? gameVersion, int? archetypeId, string? positionGroup,
        CancellationToken ct);

    /// <summary><paramref name="view"/> = "player" (padrão) ou "archetype" (uma carta por jogador × arquétipo).</summary>
    Task<PlayerCardsDto> GetCardsAsync(
        long clubId, DateOnly? from, DateOnly? to, int? gameVersion, int minMatches, int? archetypeId, string? positionGroup,
        string? view, CancellationToken ct);

    /// <summary>Com <paramref name="playerEntityId"/> só as cartas desse jogador (minMatches não as esconde).</summary>
    Task<PlayerCardsDto> GetCardsAsync(
        long clubId, DateOnly? from, DateOnly? to, int? gameVersion, int minMatches, int? archetypeId, string? positionGroup,
        string? view, long? playerEntityId, CancellationToken ct);

    /// <summary>Cada lado comparado como um arquétipo (archetypeA/archetypeB; sem eles vale archetypeId).</summary>
    Task<PlayerCompareDto?> GetCompareAsync(
        long clubId, long a, long b, DateOnly? from, DateOnly? to, int? gameVersion, int? archetypeId, int? archetypeA, int? archetypeB,
        string? positionGroup, CancellationToken ct);

    /// <summary>Compara dois jogadores do clube. Nulo quando algum deles não pertence ao clube (o controller responde 404).</summary>
    Task<PlayerCompareDto?> GetCompareAsync(
        long clubId, long a, long b, DateOnly? from, DateOnly? to, int? gameVersion, CancellationToken ct);
}
