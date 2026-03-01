namespace EAFCMatchTracker.Domain.Models;

public sealed class MembersStatsResponse
{
    public List<MemberStats> members { get; set; } = new();
    public Dictionary<string, int>? positionCount { get; set; }
}
