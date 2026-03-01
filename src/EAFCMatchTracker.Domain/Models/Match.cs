namespace EAFCMatchTracker.Domain.Models;

public class Match
{
    public required string MatchId { get; set; }
    public long Timestamp { get; set; }
    public required TimeAgo TimeAgo { get; set; }

    // ClubId como chave dinâmica
    public required Dictionary<string, Club> Clubs { get; set; }

    // ClubId -> PlayerId -> Player
    public required Dictionary<string, Dictionary<string, Player>> Players { get; set; }

    // Estatísticas agregadas por clube
    public required Dictionary<string, AggregateStats> Aggregate { get; set; }
}
