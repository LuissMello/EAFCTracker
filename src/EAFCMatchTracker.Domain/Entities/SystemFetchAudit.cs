namespace EAFCMatchTracker.Domain.Entities;

// Domain/Entities/SystemFetchAudit.cs
public class SystemFetchAudit
{
    public int Id { get; set; } = 1;                 // sempre 1 (registro singleton)
    public DateTimeOffset LastFetchedAt { get; set; } // UTC
}
