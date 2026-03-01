using System.ComponentModel.DataAnnotations;

namespace EAFCMatchTracker.Domain.Entities;

public class MatchClubEntity
{
    [Key]
    public long Id { get; set; }
    public long ClubId { get; set; }
    public long MatchId { get; set; }
    public MatchEntity Match { get; set; }
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
    public bool WinnerByDnf { get; set; }
    public short Wins { get; set; }

    public ClubDetailsEntity Details { get; set; }
}
