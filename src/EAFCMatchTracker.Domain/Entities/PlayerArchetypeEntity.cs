namespace EAFCMatchTracker.Domain.Entities;

/// <summary>
/// Catálogo editável dos arquétipos de jogador. O <see cref="Id"/> é o <c>archetypeid</c> que a EA grava em cada
/// partida (<c>MatchPlayers.Archetypeid</c>); a EA não manda nomes, então o administrador nomeia cada id.
/// Sem linha (ou sem nome) o rótulo exibido é "Arquétipo #id".
/// </summary>
public class PlayerArchetypeEntity
{
    public const int MaxNameLength = 40;
    public const int MaxShortNameLength = 8;
    public const int MaxPositionGroupLength = 10;

    /// <summary>archetypeid da EA (1..255); sem identity.</summary>
    public short Id { get; set; }
    public string? Name { get; set; }
    public string? ShortName { get; set; }
    /// <summary>"ATAQUE" | "MEIO" | "DEFESA" | "GOLEIRO" ou nulo (inferido pelas posições jogadas).</summary>
    public string? PositionGroup { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
