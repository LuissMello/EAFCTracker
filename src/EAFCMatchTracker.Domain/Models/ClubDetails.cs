namespace EAFCMatchTracker.Domain.Models;

public class ClubDetails
{
    public string Name { get; set; }
    public long ClubId { get; set; }
    public long RegionId { get; set; }
    public long TeamId { get; set; }
    public CustomKit CustomKit { get; set; }
}
