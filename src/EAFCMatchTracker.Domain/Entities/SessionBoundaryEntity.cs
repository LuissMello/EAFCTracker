namespace EAFCMatchTracker.Domain.Entities;

/// <summary>Exceção manual para a fronteira imediatamente anterior a uma partida do clube.</summary>
public class SessionBoundaryEntity
{
    public long ClubId { get; set; }
    public long MatchId { get; set; }
    public bool StartNewSession { get; set; }
}
