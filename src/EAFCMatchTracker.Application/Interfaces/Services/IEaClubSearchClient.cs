namespace EAFCMatchTracker.Application.Interfaces.Services;

/// <summary>Um clube devolvido pela busca da EA (allTimeLeaderboard/search), já com o overall embutido.</summary>
public sealed record EaClubInfo(
    long ClubId,
    string Name,
    int? CurrentDivision,
    int? BestDivision,
    int? ReputationTier,
    int? Points,
    int? GamesPlayed,
    int? Wins,
    int? Ties,
    int? Losses,
    int? Goals,
    int? GoalsAgainst,
    int? Promotions,
    int? Relegations,
    string? CrestAssetId,
    string? CustomCrestAssetId = null,
    int? SkillRating = null);

/// <summary>Resultado da busca por prefixo na EA. <see cref="Available"/> = false quando a EA falhou/está bloqueada.</summary>
public sealed record EaSearchResult(bool Available, IReadOnlyList<EaClubInfo> Items)
{
    /// <summary>A EA corta a lista em ~12 itens: com 12 ou mais, pode haver clubes que não vieram.</summary>
    public const int TruncationThreshold = 12;

    public bool Truncated => Items.Count >= TruncationThreshold;

    public static EaSearchResult Unavailable { get; } = new(false, Array.Empty<EaClubInfo>());
}

public interface IEaClubSearchClient
{
    /// <summary>Busca por PREFIXO do nome na EA (cache ~2 min em sucesso). Nunca lança por falha da EA.</summary>
    Task<EaSearchResult> SearchAsync(string name, CancellationToken ct);
}
