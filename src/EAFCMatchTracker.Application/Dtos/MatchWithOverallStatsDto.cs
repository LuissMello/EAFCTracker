namespace EAFCMatchTracker.Application.Dtos;

public sealed class MatchWithOverallStatsDto
{
    public long MatchId { get; init; }
    public DateTime Date { get; init; }
    public MatchClubOverallDto OurClub { get; init; } = null!;
    public MatchClubOverallDto Opponent { get; init; } = null!;
}

public sealed class MatchClubOverallDto
{
    public long ClubId { get; init; }
    public string? ClubName { get; init; }
    public short Goals { get; init; }
    public short Result { get; init; }
    public ClubOverallStatsDto? OverallStats { get; init; }
}
