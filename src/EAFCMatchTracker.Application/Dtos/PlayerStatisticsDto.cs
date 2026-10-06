namespace EAFCMatchTracker.Application.Dtos;

public class PlayerStatisticsDto
{
    public DateTime Date { get; set; }
    public long PlayerId { get; set; }
    public long PlayerEntityId { get; set; }
    public string PlayerName { get; set; }
    public long ClubId { get; set; }

    public int MatchesPlayed { get; set; }
    public int TotalGoals { get; set; }
    public int TotalGoalsConceded { get; set; }
    public int TotalAssists { get; set; }
    public int TotalPreAssists { get; set; }
    public int TotalShots { get; set; }
    public int TotalPassesMade { get; set; }
    public int TotalPassAttempts { get; set; }
    public int TotalTacklesMade { get; set; }
    public int TotalTackleAttempts { get; set; }
    public int TotalWins { get; set; }
    public int TotalLosses { get; set; }
    public int TotalDraws { get; set; }
    public int TotalCleanSheets { get; set; }
    public int TotalRedCards { get; set; }
    public int TotalSaves { get; set; }
    public bool HasGoalkeeperAppearance { get; set; }
    public int TotalMom { get; set; }

    public double AvgRating { get; set; }

    public double PassAccuracyPercent { get; set; }
    public double TackleSuccessPercent { get; set; }
    public double GoalAccuracyPercent { get; set; }
    public double WinPercent { get; set; }
    public string? ProOverallStr { get; set; }
    public int? ProHeight { get; set; }
    public string? ProName { get; set; }
    public bool Disconnected { get; set; }
    public int TotalSecondsPlayed { get; set; }
    public int TotalGameTime { get; set; }

    /// <summary>Id do arquétipo principal no recorte (mais jogos; empate = o mais recente); 0 = sem dado.</summary>
    public short ArchetypeId { get; set; }
    /// <summary>Arquétipo principal no recorte; nulo quando não há dado (id 0).</summary>
    public ArchetypeRef? Archetype { get; set; }
    /// <summary>Uso de cada arquétipo no recorte, do mais usado para o menos usado (vazio sem dado).</summary>
    public List<ArchetypeUsage> Archetypes { get; set; } = new();

    /// <summary>
    /// Uma linha por combinação (arquétipo, grupo da posição) das partidas deste recorte; só preenchido quando o jogador teve MAIS
    /// de uma combinação (vazio caso contrário). Mesmos campos numéricos da linha principal, calculados só sobre o subconjunto
    /// (somas = linha principal; médias/percentuais recalculados). Mais jogos primeiro.
    /// </summary>
    public List<PlayerStatisticsDto> Segments { get; set; } = new();
    /// <summary>Só nos segmentos: posição mais usada no segmento (a mais recente em empate) e seu grupo (ATAQUE/MEIO/DEFESA/GOLEIRO).</summary>
    public string? Position { get; set; }
    public string? PositionGroup { get; set; }
}
