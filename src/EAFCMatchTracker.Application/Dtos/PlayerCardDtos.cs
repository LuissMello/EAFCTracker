namespace EAFCMatchTracker.Application.Dtos;

/// <summary>Eixos (0..99) da carta. <c>Ata</c> é nulo para goleiros; <c>Gol</c> é nulo para os demais.</summary>
public class PlayerCardAxesDto
{
    public int? Ata { get; set; }
    public int Pas { get; set; }
    public int Cri { get; set; }
    public int Def { get; set; }
    public int Imp { get; set; }
    public int Reg { get; set; }
    public int? Gol { get; set; }
}

public class PlayerCardStatsDto
{
    public int Goals { get; set; }
    public int Assists { get; set; }
    public int PreAssists { get; set; }
    public double GoalsPerMatch { get; set; }
    public double AssistsPerMatch { get; set; }
    public double ShotAccuracyPct { get; set; }
    public double PassAccuracyPct { get; set; }
    public double TackleAccuracyPct { get; set; }
    /// <summary>Só para goleiros.</summary>
    public double? SavePct { get; set; }
    public double AvgRating { get; set; }
    public int Motm { get; set; }
    public int RedCards { get; set; }
    /// <summary>Só para goleiros.</summary>
    public int? CleanSheets { get; set; }
}

public class PlayerCardAttributesDto
{
    public int? Overall { get; set; }
    public string? Height { get; set; }
}

public class PlayerCardDto
{
    public long PlayerEntityId { get; set; }
    public string Name { get; set; } = "";
    public string Position { get; set; } = "";
    /// <summary>"ATAQUE" | "MEIO" | "DEFESA" | "GOLEIRO".</summary>
    public string PositionGroup { get; set; } = "MEIO";
    public int Matches { get; set; }
    public int Minutes { get; set; }
    /// <summary>"bronze" | "prata" | "ouro" | "elite".</summary>
    public string Tier { get; set; } = "bronze";
    public int Overall { get; set; }
    public bool Provisional { get; set; }
    public PlayerCardAxesDto Axes { get; set; } = new();
    public PlayerCardStatsDto Stats { get; set; } = new();
    /// <summary>Últimas 5 notas, da mais recente para a mais antiga.</summary>
    public List<double> Form { get; set; } = new();
    public PlayerCardAttributesDto? Attributes { get; set; }
    public DateTime? LastPlayedAt { get; set; }
}

public class PlayerCardsDto
{
    public long ClubId { get; set; }
    public string ClubName { get; set; } = "";
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public int? GameVersion { get; set; }
    public int TotalMatches { get; set; }
    public int MinMatches { get; set; }
    public List<PlayerCardDto> Cards { get; set; } = new();
}

public class PlayerCompareMetricDto
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public double? A { get; set; }
    public double? B { get; set; }
    public bool HigherIsBetter { get; set; }
    /// <summary>"a" | "b" | "tie" | null (sem dado em um dos lados).</summary>
    public string? Winner { get; set; }
}

public class PlayerRatingPointDto
{
    public long MatchId { get; set; }
    public DateTime Timestamp { get; set; }
    public double? A { get; set; }
    public double? B { get; set; }
}

public class PlayerCompareDto
{
    public long ClubId { get; set; }
    public PlayerCardDto A { get; set; } = new();
    public PlayerCardDto B { get; set; } = new();
    public List<PlayerCompareMetricDto> Metrics { get; set; } = new();
    /// <summary>Resultados do clube nas partidas em que os DOIS jogaram; nulo se nunca jogaram juntos.</summary>
    public LabStatsDto? Together { get; set; }
    /// <summary>Resultados do clube quando só A jogou; nulo se não houve.</summary>
    public LabStatsDto? OnlyA { get; set; }
    public LabStatsDto? OnlyB { get; set; }
    public List<PlayerRatingPointDto> RatingSeries { get; set; } = new();
}
