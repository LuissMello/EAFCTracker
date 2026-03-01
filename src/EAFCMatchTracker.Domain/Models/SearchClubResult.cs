namespace EAFCMatchTracker.Domain.Models;

public sealed class SearchClubResult
{
    public string clubId { get; set; } = default!;
    public string wins { get; set; } = default!;
    public string losses { get; set; } = default!;
    public string ties { get; set; } = default!;
    public string gamesPlayed { get; set; } = default!;
    public string gamesPlayedPlayoff { get; set; } = default!;
    public string goals { get; set; } = default!;
    public string goalsAgainst { get; set; } = default!;
    public string cleanSheets { get; set; } = default!;
    public string points { get; set; } = default!;
    public string reputationtier { get; set; } = default!;
    public string promotions { get; set; } = default!;
    public string relegations { get; set; } = default!;
    public string bestDivision { get; set; } = default!;
    public SearchClubInfo clubInfo { get; set; } = default!;
    public string platform { get; set; } = default!;
    public string clubName { get; set; } = default!;
    public string currentDivision { get; set; } = default!;
}
