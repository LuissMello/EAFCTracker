namespace EAFCMatchTracker.Application.Dtos;

public sealed class MatchResultDto
{
    public long MatchId { get; set; }
    public DateTime Timestamp { get; set; }

    /// <summary>Edição do jogo da partida (ex.: 26). Nulo se desconhecida.</summary>
    public int? GameVersion { get; set; }

    /// <summary>Registro de gols (GoalRegistrations) vinculado à partida, se houver.</summary>
    public long? GoalRegistrationId { get; set; }

    public string ClubAName { get; set; } = default!;
    public short ClubAGoals { get; set; }
    public short ClubARedCards { get; set; }  // mantido por retrocompatibilidade
    public int ClubAPlayerCount { get; set; }
    public ClubDetailsDto? ClubADetails { get; set; }
    public ClubMatchSummaryDto ClubASummary { get; set; } = new();

    public string ClubBName { get; set; } = default!;
    public short ClubBGoals { get; set; }
    public short ClubBRedCards { get; set; }  // mantido por retrocompatibilidade
    public int ClubBPlayerCount { get; set; }
    public ClubDetailsDto? ClubBDetails { get; set; }
    public ClubMatchSummaryDto ClubBSummary { get; set; } = new();

    public string ResultText { get; set; } = default!;
}
