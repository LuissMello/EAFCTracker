using EAFCMatchTracker.Application.Services;
using EAFCMatchTracker.Domain.Entities;
using EAFCMatchTracker.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EAFCMatchTracker.UnitTests;

public class ClubSessionServiceTests
{
    [Fact]
    public void GroupsAcrossMidnightUsingTheFirstMatchDate()
    {
        var matches = new[]
        {
            new SessionMatch(1, new DateTime(2026, 9, 29, 1, 0, 0, DateTimeKind.Utc)), // segunda 22h em São Paulo
            new SessionMatch(2, new DateTime(2026, 9, 29, 4, 0, 0, DateTimeKind.Utc))  // terça 01h
        };

        var sessions = ClubSessionService.Group(355651, matches, "America/Sao_Paulo", 180);

        Assert.Single(sessions);
        Assert.Equal(new DateOnly(2026, 9, 28), sessions[0].Date);
        Assert.Equal(new long[] { 1, 2 }, sessions[0].MatchIds);
    }

    [Fact]
    public void SeparatesSessionsAfterConfiguredGapAndHonorsManualOverrides()
    {
        var matches = new[]
        {
            new SessionMatch(1, new DateTime(2026, 9, 28, 22, 0, 0, DateTimeKind.Utc)),
            new SessionMatch(2, new DateTime(2026, 9, 28, 23, 0, 0, DateTimeKind.Utc)),
            new SessionMatch(3, new DateTime(2026, 9, 29, 3, 0, 0, DateTimeKind.Utc))
        };

        Assert.Equal(2, ClubSessionService.Group(355651, matches, "UTC", 120).Count);
        Assert.Equal(3, ClubSessionService.Group(355651, matches, "UTC", 120,
            new Dictionary<long, bool> { [2] = true }).Count);
        Assert.Single(ClubSessionService.Group(355651, matches, "UTC", 120,
            new Dictionary<long, bool> { [3] = false }));
    }

    [Fact]
    public void IsolatesAnAfterMidnightMatchWithoutPreviousGame()
    {
        var sessions = ClubSessionService.Group(1,
            new[] { new SessionMatch(5, new DateTime(2026, 9, 29, 4, 0, 0, DateTimeKind.Utc)) },
            "America/Sao_Paulo", 120);

        Assert.Equal(new DateOnly(2026, 9, 29), Assert.Single(sessions).Date);
    }

    [Fact]
    public async Task CalendarKeepsAnOvernightSessionOnItsStartDateAndPreservesCivilView()
    {
        var options = new DbContextOptionsBuilder<EAFCContext>()
            .UseInMemoryDatabase($"sessions-{Guid.NewGuid()}").Options;
        await using var db = new EAFCContext(options);
        var instants = new[]
        {
            new DateTime(2026, 9, 29, 1, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 29, 2, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 29, 3, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 29, 4, 0, 0, DateTimeKind.Utc)
        };
        foreach (var (instant, index) in instants.Select((value, index) => (value, index)))
        {
            db.Matches.Add(new MatchEntity
            {
                MatchId = index + 1, Timestamp = instant,
                MatchPlayers = new List<MatchPlayerEntity>(),
                Clubs = new List<MatchClubEntity>
                {
                    new() { ClubId = 355651, Team = 1, Goals = 2, Details = new ClubDetailsEntity { Name = "Meu clube" } },
                    new() { ClubId = 999, Team = 2, Goals = 1, Details = new ClubDetailsEntity { Name = "Adversário" } }
                }
            });
        }
        await db.SaveChangesAsync();

        var service = new CalendarService(db, NullLogger<CalendarService>.Instance, new ClubSessionService(db));
        var sessionMonth = await service.GetMonthlyCalendarAsync(2026, 9, new HashSet<long> { 355651 }, true, default);
        var civilMonth = await service.GetMonthlyCalendarAsync(2026, 9, new HashSet<long> { 355651 }, false, default);
        var details = await service.GetDayDetailsAsync(new DateOnly(2026, 9, 28), new HashSet<long> { 355651 }, true, default);

        Assert.Equal(4, Assert.Single(sessionMonth.Days).MatchesCount);
        Assert.Equal(new DateOnly(2026, 9, 28), sessionMonth.Days[0].Date);
        Assert.Equal(2, civilMonth.Days.Count);
        Assert.Equal(4, details.Matches.Count);
        Assert.Single(details.Sessions);
    }

    [Fact]
    public void SessionMigrationIsRegistered()
    {
        var options = new DbContextOptionsBuilder<EAFCContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused").Options;
        using var db = new EAFCContext(options);
        Assert.Contains("20260929175400_AddClubSessions", db.Database.GetMigrations());
        var sql = db.GetService<IMigrator>().GenerateScript(
            "20260928221334_GameVersionsEIndices", "20260929175400_AddClubSessions");
        Assert.Contains("CREATE TABLE \"SessionBoundaries\"", sql);
        Assert.Contains("SessionGapMinutes", sql);
    }
}
