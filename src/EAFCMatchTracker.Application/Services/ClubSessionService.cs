using EAFCMatchTracker.Domain.Entities;
using EAFCMatchTracker.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EAFCMatchTracker.Application.Services;

public sealed record SessionMatch(long MatchId, DateTime Timestamp);

public sealed class ClubSession
{
    public long ClubId { get; init; }
    public long Id { get; init; }
    public DateOnly Date { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime EndedAt { get; set; }
    public List<long> MatchIds { get; } = new();
}

/// <summary>Fonte única para agrupar partidas em sessões, sempre por clube e na ordem temporal.</summary>
public sealed class ClubSessionService
{
    private readonly EAFCContext _db;

    public ClubSessionService(EAFCContext db) => _db = db;

    public async Task<List<ClubSession>> GetForClubAsync(long clubId, CancellationToken ct)
    {
        var settings = await _db.TrackedClubs.AsNoTracking().FirstOrDefaultAsync(c => c.ClubId == clubId, ct);
        var matches = await _db.MatchClubs.AsNoTracking()
            .Where(c => c.ClubId == clubId)
            .Select(c => new SessionMatch(c.MatchId, c.Match.Timestamp))
            .OrderBy(m => m.Timestamp).ThenBy(m => m.MatchId)
            .ToListAsync(ct);
        var boundaries = await _db.SessionBoundaries.AsNoTracking()
            .Where(b => b.ClubId == clubId)
            .ToDictionaryAsync(b => b.MatchId, b => b.StartNewSession, ct);

        return Group(clubId, matches, settings?.TimeZoneId ?? "America/Sao_Paulo",
            settings?.SessionGapMinutes ?? 120, boundaries);
    }

    public static List<ClubSession> Group(long clubId, IEnumerable<SessionMatch> matches,
        string timeZoneId, int gapMinutes, IReadOnlyDictionary<long, bool>? boundaries = null)
    {
        var zone = ResolveZone(timeZoneId);
        var gap = TimeSpan.FromMinutes(gapMinutes);
        var sessions = new List<ClubSession>();
        ClubSession? current = null;
        DateTime? previous = null;

        foreach (var match in matches.OrderBy(m => m.Timestamp).ThenBy(m => m.MatchId))
        {
            var timestamp = DateTime.SpecifyKind(match.Timestamp, DateTimeKind.Utc);
            var starts = false;
            var forced = boundaries?.TryGetValue(match.MatchId, out starts) == true;
            if (current is null || (forced ? starts : timestamp - previous!.Value > gap))
            {
                current = new ClubSession
                {
                    ClubId = clubId,
                    Id = match.MatchId,
                    Date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(timestamp, zone)),
                    StartedAt = timestamp,
                    EndedAt = timestamp
                };
                sessions.Add(current);
            }
            current.MatchIds.Add(match.MatchId);
            current.EndedAt = timestamp;
            previous = timestamp;
        }
        return sessions;
    }

    public static TimeZoneInfo ResolveZone(string id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (TimeZoneNotFoundException) { return FallbackZone(id); }
        catch (InvalidTimeZoneException) { return FallbackZone(id); }
    }

    private static TimeZoneInfo FallbackZone(string id) => id == "America/Sao_Paulo"
        ? TimeZoneInfo.CreateCustomTimeZone("BRT", TimeSpan.FromHours(-3), "Brasília", "Brasília")
        : TimeZoneInfo.Utc;
}
