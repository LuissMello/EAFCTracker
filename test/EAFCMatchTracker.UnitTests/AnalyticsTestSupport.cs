using EAFCMatchTracker.Application.Services;
using EAFCMatchTracker.Application.Services.Analytics;
using EAFCMatchTracker.Domain.Entities;
using EAFCMatchTracker.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace EAFCMatchTracker.UnitTests;

/// <summary>Jogador do nosso clube numa partida semeada.</summary>
internal sealed record Pl(long Id, int Goals = 0, int Assists = 0, double Rating = 7.0, bool Mom = false, int Reds = 0, int Pre = 0, string Pos = "forward");

/// <summary>Banco em memória + semeadura de partidas para os testes das páginas analíticas.</summary>
internal sealed class AnalyticsSeed : IDisposable
{
    public const long Club = 100;
    public const long Rival = 200;
    public const int V26 = 2; // GameVersions.Id de FC26 (semeado por HasData)
    public const int V27 = 3; // FC27

    public EAFCContext Db { get; }
    public IMemoryCache Cache { get; } = new MemoryCache(new MemoryCacheOptions());
    private long _next = 1;

    public AnalyticsSeed(string timeZone = "America/Sao_Paulo", int gapMinutes = 120)
    {
        Db = new EAFCContext(new DbContextOptionsBuilder<EAFCContext>().UseInMemoryDatabase($"analytics-{Guid.NewGuid()}").Options);
        Db.Database.EnsureCreated();
        var tracked = Db.TrackedClubs.Find(Club);
        if (tracked is null) Db.TrackedClubs.Add(new TrackedClubEntity { ClubId = Club, TimeZoneId = timeZone, SessionGapMinutes = gapMinutes });
        else { tracked.TimeZoneId = timeZone; tracked.SessionGapMinutes = gapMinutes; }
        Db.SaveChanges();
    }

    public GameNightService Nights() => new(Db, new ClubSessionService(Db), new MemoryCache(new MemoryCacheOptions()));
    public LabService Lab() => new(Db, new ClubSessionService(Db), new MemoryCache(new MemoryCacheOptions()));
    public WrappedService Wrapped() => new(Db, new ClubSessionService(Db), new MemoryCache(new MemoryCacheOptions()));

    public static readonly DateTime Base = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    public PlayerEntity Player(long id, string name, long clubId = Club)
    {
        var existing = Db.Players.Local.FirstOrDefault(p => p.Id == id);
        if (existing is not null) return existing;
        var p = new PlayerEntity { Id = id, PlayerId = 9000 + id, ClubId = clubId, Playername = name };
        Db.Players.Add(p);
        Db.SaveChanges();
        return p;
    }

    /// <summary>Insere uma partida do clube de teste contra um rival (com snapshots de SR opcionais).</summary>
    public long Match(
        DateTime ts, int gf, int ga, long? id = null, long clubId = Club, long oppId = Rival, string oppName = "Rival FC",
        int? version = V27, Pl[]? ours = null, int oppPlayers = 0, int? ourSr = null, int? oppSr = null, int? division = null,
        long teamId = 45, string? ourSrText = null)
    {
        var matchId = id ?? _next++;
        if (id.HasValue) _next = Math.Max(_next, id.Value + 1);
        var utc = DateTime.SpecifyKind(ts, DateTimeKind.Utc);
        Db.Matches.Add(new MatchEntity { MatchId = matchId, Timestamp = utc, GameVersionId = version });
        Db.MatchClubs.Add(new MatchClubEntity
        {
            MatchId = matchId, ClubId = clubId, Goals = (short)gf, GoalsAgainst = (short)ga, Team = 1,
            Details = new ClubDetailsEntity { ClubId = clubId, Name = "Meu Clube", TeamId = 10 }
        });
        Db.MatchClubs.Add(new MatchClubEntity
        {
            MatchId = matchId, ClubId = oppId, Goals = (short)ga, GoalsAgainst = (short)gf, Team = 2,
            Details = new ClubDetailsEntity { ClubId = oppId, Name = oppName, TeamId = teamId, CrestAssetId = "777" }
        });

        foreach (var p in ours ?? Array.Empty<Pl>())
        {
            Player(p.Id, $"Jogador {p.Id}");
            Db.MatchPlayers.Add(new MatchPlayerEntity
            {
                MatchId = matchId, ClubId = clubId, PlayerEntityId = p.Id, Goals = (short)p.Goals, Assists = (short)p.Assists,
                PreAssists = (short)p.Pre, Rating = p.Rating, Mom = p.Mom, Redcards = (short)p.Reds, Pos = p.Pos,
                ProName = $"Pro {p.Id}", Realtimegame = "", Realtimeidle = "", Vproattr = "", Vprohackreason = "",
                MatchEventAggregate0 = "", MatchEventAggregate1 = "", MatchEventAggregate2 = "", MatchEventAggregate3 = ""
            });
        }
        for (var i = 0; i < oppPlayers; i++)
        {
            Db.MatchPlayers.Add(new MatchPlayerEntity
            {
                MatchId = matchId, ClubId = oppId, PlayerEntityId = 50_000 + oppId * 10 + i, Pos = "midfielder",
                Realtimegame = "", Realtimeidle = "", Vproattr = "", Vprohackreason = "",
                MatchEventAggregate0 = "", MatchEventAggregate1 = "", MatchEventAggregate2 = "", MatchEventAggregate3 = ""
            });
        }

        if (ourSr.HasValue || ourSrText is not null || division.HasValue)
            Db.OverallStats.Add(new OverallStatsEntity
            {
                ClubId = clubId, MatchId = matchId, SkillRating = ourSrText ?? ourSr?.ToString(), CurrentDivision = division,
                GameVersionId = version, UpdatedAtUtc = utc
            });
        if (oppSr.HasValue)
            Db.OverallStats.Add(new OverallStatsEntity
            {
                ClubId = oppId, MatchId = matchId, SkillRating = oppSr.ToString(), GameVersionId = version, UpdatedAtUtc = utc
            });
        Db.SaveChanges();
        return matchId;
    }

    public void Goal(long matchId, long scorer, long? assist = null, long? pre = null, long clubId = Club)
    {
        Db.MatchGoalLinks.Add(new MatchGoalLinkEntity
        {
            MatchId = matchId, ClubId = clubId, ScorerPlayerEntityId = scorer, AssistPlayerEntityId = assist, PreAssistPlayerEntityId = pre
        });
        Db.SaveChanges();
    }

    public void Dispose() => Db.Dispose();
}
