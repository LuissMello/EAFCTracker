namespace EAFCMatchTracker.Application.Dtos;

public class MatchClubDto
{
    public long ClubId { get; set; }
    public DateTime Date { get; set; }
    public int GameNumber { get; set; }
    public short Goals { get; set; }
    public short GoalsAgainst { get; set; }
    public short Losses { get; set; }
    public short MatchType { get; set; }
    public short Result { get; set; }
    public short Score { get; set; }
    public short SeasonId { get; set; }
    public int Team { get; set; }
    public short Ties { get; set; }
    public short Wins { get; set; }
    public bool WinnerByDnf { get; set; }

    public ClubDetailsDto Details { get; set; }
}
