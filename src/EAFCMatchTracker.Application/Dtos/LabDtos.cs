namespace EAFCMatchTracker.Application.Dtos;

/// <summary>Bloco de estatísticas de um conjunto de partidas, do ponto de vista do nosso clube.</summary>
public class LabStatsDto
{
    public int Matches { get; set; }
    public int Wins { get; set; }
    public int Draws { get; set; }
    public int Losses { get; set; }
    public double WinRatePct { get; set; }
    public double PointsPerMatch { get; set; }
    public double GoalsForPerMatch { get; set; }
    public double GoalsAgainstPerMatch { get; set; }
}

public class LabPlayerDeltaDto
{
    public double WinRatePct { get; set; }
    public double PointsPerMatch { get; set; }
    public double GoalDiffPerMatch { get; set; }
}

public class LabPlayerImpactItemDto
{
    public long PlayerEntityId { get; set; }
    public string Name { get; set; } = "";
    public LabStatsDto With { get; set; } = new();
    public LabStatsDto? Without { get; set; }
    public LabPlayerDeltaDto? Delta { get; set; }
    public string Reliability { get; set; } = "low";
    public string? Note { get; set; }
}

public class LabPlayerImpactDto
{
    public long ClubId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public int TotalMatches { get; set; }
    public LabStatsDto Baseline { get; set; } = new();
    public List<LabPlayerImpactItemDto> Players { get; set; } = new();
}

public class LabWeekdayBucketDto : LabStatsDto { public int Weekday { get; set; } }
public class LabHourBucketDto : LabStatsDto { public int Hour { get; set; } }
public class LabSessionPositionBucketDto : LabStatsDto { public int Position { get; set; } }
public class LabPlayersBucketDto : LabStatsDto { public int Players { get; set; } }

public class LabOpponentStrengthBucketDto : LabStatsDto
{
    public string Band { get; set; } = "";
    public int SrGapMin { get; set; }
    public int SrGapMax { get; set; }
}

public class LabContextDto
{
    public long ClubId { get; set; }
    public string TimeZoneId { get; set; } = "";
    public LabStatsDto Baseline { get; set; } = new();
    public List<LabWeekdayBucketDto> ByWeekday { get; set; } = new();
    public List<LabHourBucketDto> ByHour { get; set; } = new();
    public List<LabSessionPositionBucketDto> BySessionPosition { get; set; } = new();
    public List<LabPlayersBucketDto> ByOurPlayers { get; set; } = new();
    public List<LabPlayersBucketDto> ByOpponentPlayers { get; set; } = new();
    public List<LabOpponentStrengthBucketDto> ByOpponentStrength { get; set; } = new();
}

public class LabDuoDeltaDto
{
    public double WinRatePct { get; set; }
    public double PointsPerMatch { get; set; }
}

public class LabDuoDto
{
    public long APlayerEntityId { get; set; }
    public string AName { get; set; } = "";
    public long BPlayerEntityId { get; set; }
    public string BName { get; set; } = "";
    public LabStatsDto Together { get; set; } = new();
    public LabStatsDto? Apart { get; set; }
    public LabDuoDeltaDto? Delta { get; set; }
    public string Reliability { get; set; } = "low";
}

public class LabDuosDto
{
    public List<LabDuoDto> Best { get; set; } = new();
    public List<LabDuoDto> Worst { get; set; } = new();
}
