namespace EAFCMatchTracker.Domain.Models;

public sealed class SearchClubInfo
{
    public string name { get; set; } = default!;
    public long clubId { get; set; }
    public long regionId { get; set; }
    public long teamId { get; set; }
    public CustomKit? customKit { get; set; }
}
