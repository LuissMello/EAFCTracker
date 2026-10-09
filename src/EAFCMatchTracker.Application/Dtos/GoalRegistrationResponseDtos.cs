namespace EAFCMatchTracker.Application.Dtos;

/// <summary>Corpo de POST /api/goal-registrations. <see cref="Goals"/> é opcional (0..40).</summary>
public class CreateGoalRegistrationRequest
{
    public long ClubId { get; set; }
    public long OpponentClubId { get; set; }
    public string? OpponentName { get; set; }
    public string? Notes { get; set; }
    public List<GoalRegistrationDto>? Goals { get; set; }
}

/// <summary>Corpo de PUT /api/goal-registrations/{id}: substitui a lista de gols (e opcionalmente adversário/notas).</summary>
public class UpdateGoalRegistrationRequest
{
    public long? OpponentClubId { get; set; }
    public string? OpponentName { get; set; }
    public string? Notes { get; set; }
    public List<GoalRegistrationDto>? Goals { get; set; }
}

public class GoalRegistrationLineResponseDto
{
    public long Id { get; set; }
    public int Order { get; set; }
    public long ScorerPlayerEntityId { get; set; }
    public string? ScorerName { get; set; }
    public long? AssistPlayerEntityId { get; set; }
    public string? AssistName { get; set; }
    public long? PreAssistPlayerEntityId { get; set; }
    public string? PreAssistName { get; set; }
}

/// <summary>Partida sugerida pelo linker para confirmação (o adversário REAL dela, placar do nosso lado).</summary>
public class GoalRegistrationSuggestedMatchDto
{
    public long MatchId { get; set; }
    public DateTime PlayedAt { get; set; }
    public long OpponentClubId { get; set; }
    public string OpponentName { get; set; } = string.Empty;
    public int OurGoals { get; set; }
    public int TheirGoals { get; set; }

    /// <summary>True quando <see cref="OurGoals"/> é igual ao nº de gols registrados.</summary>
    public bool GoalsMatch { get; set; }
}

public class GoalRegistrationResponseDto
{
    public long Id { get; set; }
    public long ClubId { get; set; }
    public long OpponentClubId { get; set; }
    public string OpponentName { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>Mesmo valor de <see cref="CreatedAt"/> (início do registro ao vivo).</summary>
    public DateTime StartedAt { get; set; }

    /// <summary>"Pending" | "Linked" | "NeedsReview" | "Expired".</summary>
    public string Status { get; set; } = string.Empty;
    public long? MatchId { get; set; }
    public DateTime? LinkedAt { get; set; }
    public string? ReviewNote { get; set; }

    /// <summary>UTC. Preenchido quando o usuário finalizou o registro (POST /{id}/finish).</summary>
    public DateTime? FinishedAt { get; set; }

    /// <summary>Partida sugerida (outro adversário) aguardando confirmação; nula sem sugestão.</summary>
    public GoalRegistrationSuggestedMatchDto? SuggestedMatch { get; set; }
    public int GoalsCount { get; set; }
    public List<GoalRegistrationLineResponseDto> Goals { get; set; } = new();
}

public class GoalRosterPlayerDto
{
    public long PlayerEntityId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Position { get; set; }
    public int MatchesPlayed { get; set; }
    public DateTime? LastPlayedAt { get; set; }
    public bool Active { get; set; }
}

public class GoalRosterResponseDto
{
    public long ClubId { get; set; }
    public List<GoalRosterPlayerDto> Players { get; set; } = new();
}

public class OpponentSearchRecordDto
{
    public int Games { get; set; }
    public int Wins { get; set; }
    public int Draws { get; set; }
    public int Losses { get; set; }
}

public class OpponentSearchItemDto
{
    public long ClubId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? CurrentDivision { get; set; }
    public int? ReputationTier { get; set; }

    /// <summary>teamId do clube (mesmo valor de <see cref="CrestAssetId"/>, como número) para distinguir clubes de nome parecido.</summary>
    public long? TeamId { get; set; }

    /// <summary>Divisão atual (alias de <see cref="CurrentDivision"/>).</summary>
    public int? Division { get; set; }

    /// <summary>Skill rating, quando a busca da EA o traz; nulo caso contrário.</summary>
    public int? SkillRating { get; set; }

    /// <summary>Identificador do escudo no CDN da EA = teamId do clube (o mesmo que o resto do site usa).</summary>
    public string? CrestAssetId { get; set; }

    /// <summary>Escudo alternativo (crest personalizado do kit) para quando o teamId não tem imagem no CDN.</summary>
    public string? CustomCrestAssetId { get; set; }

    /// <summary>Retrospecto vindo da própria busca da EA (nulo quando a origem é só o histórico).</summary>
    public OpponentSearchRecordDto? Record { get; set; }

    /// <summary>"history" | "ea" | "both".</summary>
    public string Source { get; set; } = "history";
    public int TimesFaced { get; set; }
    public DateTime? LastFacedAt { get; set; }
}

public class OpponentSearchResponseDto
{
    public string Query { get; set; } = string.Empty;
    public bool EaAvailable { get; set; }

    /// <summary>True quando a EA devolveu o máximo de itens (≥ 12): pode haver mais clubes que não vieram.</summary>
    public bool EaTruncated { get; set; }
    public List<OpponentSearchItemDto> Results { get; set; } = new();
}

public class OpponentPreviewRecordDto
{
    public int Games { get; set; }
    public int Wins { get; set; }
    public int Draws { get; set; }
    public int Losses { get; set; }
    public double WinRatePct { get; set; }
}

public class OpponentPreviewGoalsDto
{
    public int For { get; set; }
    public int Against { get; set; }
    public double AvgFor { get; set; }
    public double AvgAgainst { get; set; }
}

public class OpponentPreviewRecentDto
{
    public int MatchesAnalyzed { get; set; }

    /// <summary>"W" | "D" | "L", do mais novo para o mais antigo (máx. 5).</summary>
    public List<string> Results { get; set; } = new();
    public double? AvgGoalsFor { get; set; }
    public double? AvgGoalsAgainst { get; set; }
    public int? LastMatchPlayers { get; set; }
    public double? AvgPlayersLast5 { get; set; }
    public DateTime? LastPlayedAt { get; set; }
}

public class OpponentPreviewMembersDto
{
    public int Count { get; set; }
    public double? AvgOverall { get; set; }
}

public class OpponentPreviewHeadToHeadDto
{
    public int Games { get; set; }
    public int Wins { get; set; }
    public int Draws { get; set; }
    public int Losses { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
    public DateTime? LastPlayedAt { get; set; }
}

/// <summary>Pré-estatísticas do adversário. Todo bloco é opcional (nulo quando indisponível); veja Warnings.</summary>
public class OpponentPreviewDto
{
    /// <summary>Id do clube ADVERSÁRIO (o sujeito do preview).</summary>
    public long ClubId { get; set; }
    public string? Name { get; set; }
    public string? CrestAssetId { get; set; }
    public int? CurrentDivision { get; set; }
    public int? BestDivision { get; set; }
    public int? ReputationTier { get; set; }
    public int? Points { get; set; }
    public OpponentPreviewRecordDto? Record { get; set; }
    public OpponentPreviewGoalsDto? Goals { get; set; }
    public int? Promotions { get; set; }
    public int? Relegations { get; set; }
    public OpponentPreviewRecentDto? Recent { get; set; }
    public OpponentPreviewMembersDto? Members { get; set; }
    public OpponentPreviewHeadToHeadDto? HeadToHead { get; set; }
    public List<string> Warnings { get; set; } = new();
}
