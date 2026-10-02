using EAFCMatchTracker.Application.Repositories;
using EAFCMatchTracker.Application.Services;
using EAFCMatchTracker.Domain.Entities;
using EAFCMatchTracker.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace EAFCMatchTracker.UnitTests;

internal sealed class FakeTimeProvider : TimeProvider
{
    public DateTimeOffset Now { get; set; }
    public FakeTimeProvider(DateTimeOffset now) => Now = now;
    public override DateTimeOffset GetUtcNow() => Now;
}

/// <summary>Jogador de uma partida semeada: gols/assistências reais.</summary>
internal sealed record Mp(long PlayerId, short Goals, short Assists = 0);

internal static class GoalTestSupport
{
    public const long Club = 100;
    public const long Opponent = 200;

    // 2026-10-01 20:00 UTC
    public static readonly DateTime T0 = new(2026, 10, 1, 20, 0, 0, DateTimeKind.Utc);

    public static EAFCContext NewDb() =>
        new(new DbContextOptionsBuilder<EAFCContext>().UseInMemoryDatabase($"goalreg-{Guid.NewGuid()}").Options);

    public static GoalRegistrationLinker NewLinker(EAFCContext db, FakeTimeProvider time) =>
        new(db, time, NullLogger<GoalRegistrationLinker>.Instance);

    public static GoalRegistrationService NewService(EAFCContext db, FakeTimeProvider time) =>
        new(new GoalRegistrationRepository(db), NewLinker(db, time), time, NullLogger<GoalRegistrationService>.Instance);

    public static PlayerEntity AddPlayer(EAFCContext db, long id, long clubId, string name)
    {
        var p = new PlayerEntity { Id = id, PlayerId = 9000 + id, ClubId = clubId, Playername = name };
        db.Players.Add(p);
        return p;
    }

    /// <summary>Partida entre <see cref="Club"/> e <see cref="Opponent"/>; jogadores listados são do nosso clube.</summary>
    public static MatchEntity AddMatch(
        EAFCContext db, long matchId, DateTime timestamp, short clubGoals, short opponentGoals, params Mp[] ourPlayers)
    {
        var match = new MatchEntity
        {
            MatchId = matchId,
            Timestamp = timestamp,
            Clubs = new List<MatchClubEntity>
            {
                new() { ClubId = Club, Team = 1, Goals = clubGoals, GoalsAgainst = opponentGoals, Details = new ClubDetailsEntity { Name = "Meu clube", ClubId = Club } },
                new() { ClubId = Opponent, Team = 2, Goals = opponentGoals, GoalsAgainst = clubGoals, Details = new ClubDetailsEntity { Name = "Adversário FC", ClubId = Opponent } }
            },
            MatchPlayers = ourPlayers.Select(p => new MatchPlayerEntity
            {
                MatchId = matchId,
                ClubId = Club,
                PlayerEntityId = p.PlayerId,
                Goals = p.Goals,
                Assists = p.Assists,
                ProName = $"Jogador {p.PlayerId}",
                Pos = "forward",
                Realtimegame = "", Realtimeidle = "", Vproattr = "", Vprohackreason = "",
                MatchEventAggregate0 = "", MatchEventAggregate1 = "", MatchEventAggregate2 = "", MatchEventAggregate3 = ""
            }).ToList()
        };
        db.Matches.Add(match);
        return match;
    }

    public static GoalRegistrationEntity AddRegistration(
        EAFCContext db, DateTime createdAt, params (long scorer, long? assist, long? pre)[] goals)
    {
        var reg = new GoalRegistrationEntity
        {
            ClubId = Club,
            OpponentClubId = Opponent,
            OpponentName = "Adversário FC",
            CreatedAt = createdAt,
            Status = GoalRegistrationStatus.Pending,
            Goals = goals.Select((g, i) => new GoalRegistrationGoalEntity
            {
                Order = i + 1,
                ScorerPlayerEntityId = g.scorer,
                AssistPlayerEntityId = g.assist,
                PreAssistPlayerEntityId = g.pre
            }).ToList()
        };
        db.GoalRegistrations.Add(reg);
        return reg;
    }
}
