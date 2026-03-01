namespace EAFCMatchTracker.Application.Dtos;

public class MatchDto
{
    public long MatchId { get; set; }
    public DateTime Timestamp { get; set; }
    public EAFCMatchTracker.Domain.Entities.MatchType MatchType { get; set; }
    public List<MatchClubDto> Clubs { get; set; }
    public List<MatchPlayerDto> Players { get; set; }
}
