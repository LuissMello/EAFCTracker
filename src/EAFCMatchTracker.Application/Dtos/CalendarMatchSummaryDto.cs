namespace EAFCMatchTracker.Application.Dtos;

public class CalendarMatchSummaryDto
{
    public long MatchId { get; set; }
    public DateTime Timestamp { get; set; }

    // lado A
    public long ClubAId { get; set; }
    public string ClubAName { get; set; }
    public int ClubAGoals { get; set; }
    public string? ClubACrestAssetId { get; set; }

    // lado B
    public long ClubBId { get; set; }
    public string ClubBName { get; set; }
    public int ClubBGoals { get; set; }
    public string? ClubBCrestAssetId { get; set; }

    // resultado do ponto de vista do clubId consultado
    // "W" = vitória, "D" = empate, "L" = derrota
    public string ResultForClub { get; set; }

    // estatísticas agregadas do jogo (soma dos jogadores)
    public CalendarMatchStatLineDto Stats { get; set; }
}
