namespace EAFCMatchTracker.Application.Dtos;

public sealed class MatchResultDto
{
    public long MatchId { get; set; }
    public DateTime Timestamp { get; set; }

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
