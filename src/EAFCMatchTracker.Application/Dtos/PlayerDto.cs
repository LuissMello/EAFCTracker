namespace EAFCMatchTracker.Application.Dtos;

/// <summary>
/// Jogador (GET /api/players/{playerId}). Mesmos nomes de propriedade JSON que a entidade PlayerEntity tinha
/// (camelCase), sem as navegações (playerMatchStats / matchPlayers).
/// </summary>
public sealed class PlayerDto
{
    public long Id { get; init; }
    public long PlayerId { get; init; }
    public long ClubId { get; init; }
    public string Playername { get; init; } = "";
    public long? PlayerMatchStatsId { get; init; }
}
