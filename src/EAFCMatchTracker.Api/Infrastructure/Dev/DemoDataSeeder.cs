// ============================================================================================================
// TEST AID ONLY (dev/e2e). Nothing in this folder runs or is registered unless BOTH are true:
//   EAFCSettings:UseInMemoryDb=true   AND   Seed:Demo=true   (env: EAFCSettings__UseInMemoryDb / Seed__Demo)
// - DemoDataSeeder fills the IN-MEMORY database with a tracked club (355651), 12 players (8 recently active,
//   4 inactive) and 3 opponents already faced, so the "Registrar gols" screens have data to work with.
// - DevMatchSimulator + DevController (POST /api/dev/simulate-match) additionally require the Development
//   environment: they insert a finished match as the EA fetch would and then run the goal-registration linker.
// In any other configuration (production Npgsql database, Seed:Demo unset...) none of these types is
// registered and /api/dev/* answers 404 (see DevControllerFeatureProvider).
// ============================================================================================================
using EAFCMatchTracker.Application.Interfaces.Services;
using EAFCMatchTracker.Domain.Entities;
using EAFCMatchTracker.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.EntityFrameworkCore;

namespace EAFCMatchTracker.Api.Infrastructure.Dev;

public static class DemoDataSeeder
{
    public const long OurClubId = 355651;

    public static readonly (long Id, string Name)[] Opponents =
    {
        (9000001, "Shoppinho FC"),
        (9000002, "Trash As Well"),
        (9000003, "Almeria FC"),
    };

    // (name, position, played in the last 30 days?)
    private static readonly (string Name, string Pos, bool Active)[] Roster =
    {
        ("Beltrano", "forward", true), ("Fulano", "midfielder", true), ("Sicrano", "forward", true),
        ("Ciclano", "defender", true), ("Zeca", "midfielder", true), ("Tiago", "goalkeeper", true),
        ("Marcelo", "defender", true), ("Pedrinho", "midfielder", true),
        ("Veterano", "defender", false), ("Jonas", "forward", false), ("Rafa", "midfielder", false), ("Lucas", "goalkeeper", false),
    };

    internal static string PosOf(string? name) =>
        Roster.FirstOrDefault(r => r.Name == name).Pos ?? "midfielder";

    public static bool IsEnabled(IConfiguration config) =>
        config.GetValue<bool>("EAFCSettings:UseInMemoryDb") && config.GetValue<bool>("Seed:Demo");

    public static bool SimulatorEnabled(IConfiguration config, IHostEnvironment env) =>
        IsEnabled(config) && env.IsDevelopment();

    public static async Task SeedAsync(IServiceProvider services, ILogger logger)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EAFCContext>();
        if (await db.Players.AnyAsync(p => p.ClubId == OurClubId)) return;

        var versionId = await db.GameVersions.Where(v => v.IsCurrent).Select(v => (int?)v.Id).FirstOrDefaultAsync();
        var now = DateTime.UtcNow;

        var players = new List<PlayerEntity>();
        for (var i = 0; i < Roster.Length; i++)
            players.Add(new PlayerEntity { PlayerId = 7000 + i, ClubId = OurClubId, Playername = Roster[i].Name });
        db.Players.AddRange(players);
        await db.SaveChangesAsync();

        var stats = players.Select(p => new PlayerMatchStatsEntity { PlayerEntityId = p.Id }).ToList();
        db.PlayerMatchStats.AddRange(stats);
        await db.SaveChangesAsync();
        for (var i = 0; i < players.Count; i++) players[i].PlayerMatchStatsId = stats[i].Id;
        await db.SaveChangesAsync();

        // Historical matches (newest -> oldest); active players appear in recent ones, inactive only in old ones.
        var rnd = new Random(42);
        var history = new (int DaysAgo, int Opp, int Ours, int Theirs)[]
        {
            (2, 0, 3, 1), (4, 1, 2, 2), (7, 2, 1, 0), (10, 1, 4, 3), (14, 0, 0, 2),
            (21, 2, 2, 1), (28, 1, 5, 0), (45, 0, 1, 1), (100, 1, 2, 3), (95, 2, 3, 2), (110, 0, 1, 2), (118, 1, 2, 0),
        };
        for (var n = 0; n < history.Length; n++)
        {
            var h = history[n];
            // > 90 days ago: only the 4 "inactive" players (so they stay outside the 90-day active window)
            var pool = h.DaysAgo <= 90
                ? players.Where((_, i) => Roster[i].Active).ToList()
                : players.Where((_, i) => !Roster[i].Active).ToList();
            var lineup = pool.OrderBy(_ => rnd.Next()).Take(Math.Min(pool.Count, 5)).ToList();
            var goals = lineup.Select(_ => 0).ToArray();
            var assists = lineup.Select(_ => 0).ToArray();
            for (var g = 0; g < h.Ours; g++) goals[rnd.Next(lineup.Count)]++;
            for (var g = 0; g < h.Ours; g++) assists[rnd.Next(lineup.Count)]++;

            var opp = Opponents[h.Opp];
            var ts = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc).AddDays(-h.DaysAgo).AddHours(21).AddMinutes(rnd.Next(0, 90));
            DemoMatch.Insert(db, 8_000_000_000L + n, ts, versionId, OurClubId, "Nosso Clube (demo)", h.Ours,
                opp.Id, opp.Name, h.Theirs, lineup.Select((p, i) => (p, goals[i], assists[i])).ToList(), stats);
        }

        await db.SaveChangesAsync();
        logger.LogWarning(
            "Seed:Demo ATIVO: dados de demonstração criados no banco EM MEMÓRIA (clube {ClubId}, {Players} jogadores, {Matches} partidas).",
            OurClubId, players.Count, history.Length);
    }
}

/// <summary>Builds the same shape of rows the real fetch (ClubMatchService.SaveMatchAsync) writes. Test aid.</summary>
internal static class DemoMatch
{
    // O escudo no CDN da EA é identificado pelo teamId; ids reais para a demonstração mostrar escudos de verdade.
    private static long DemoTeamId(long clubId, long ourClubId) => clubId == ourClubId
        ? 45
        : (clubId % 3) switch { 1 => 243, 2 => 21, _ => 241 };

    public static void Insert(
        EAFCContext db, long matchId, DateTime timestampUtc, int? versionId,
        long ourClubId, string ourName, int ourGoals,
        long oppClubId, string oppName, int oppGoals,
        List<(PlayerEntity Player, int Goals, int Assists)> ourPlayers,
        List<PlayerMatchStatsEntity> statsPool)
    {
        // Same type/unit as ClubMatchService: DateTime in UTC (from Unix seconds -> whole seconds).
        var ts = DateTime.SpecifyKind(new DateTime(timestampUtc.Ticks - timestampUtc.Ticks % TimeSpan.TicksPerSecond), DateTimeKind.Utc);
        db.Matches.Add(new MatchEntity { MatchId = matchId, Timestamp = ts, MatchType = MatchType.League, GameVersionId = versionId });

        MatchClubEntity Club(long clubId, string name, int gf, int ga) => new()
        {
            MatchId = matchId, ClubId = clubId, Date = ts, GameNumber = 1, Goals = (short)gf, GoalsAgainst = (short)ga,
            Result = (short)(gf > ga ? 1 : gf < ga ? 2 : 3), Wins = (short)(gf > ga ? 1 : 0), Losses = (short)(gf < ga ? 1 : 0),
            Ties = (short)(gf == ga ? 1 : 0), MatchType = 1, SeasonId = 1, Team = clubId == ourClubId ? 1 : 2,
            CurrentDivision = 5,
            Details = new ClubDetailsEntity { ClubId = clubId, Name = name, CrestAssetId = "999001", TeamId = DemoTeamId(clubId, ourClubId) }
        };
        db.MatchClubs.Add(Club(ourClubId, ourName, ourGoals, oppGoals));
        db.MatchClubs.Add(Club(oppClubId, oppName, oppGoals, ourGoals));

        foreach (var (player, goals, assists) in ourPlayers)
        {
            var stat = statsPool.FirstOrDefault(s => s.PlayerEntityId == player.Id);
            db.MatchPlayers.Add(new MatchPlayerEntity
            {
                MatchId = matchId, ClubId = ourClubId, PlayerEntityId = player.Id,
                PlayerMatchStatsEntityId = stat?.Id ?? 0, Goals = (short)goals, Assists = (short)assists,
                Pos = DemoDataSeeder.PosOf(player.Playername), Rating = 7.0, Realtimegame = "", Realtimeidle = "", Vproattr = "", Vprohackreason = "",
                MatchEventAggregate0 = "", MatchEventAggregate1 = "", MatchEventAggregate2 = "", MatchEventAggregate3 = "",
                ProName = player.Playername, SecondsPlayed = 5400, Score = (short)goals,
            });
        }
    }
}

/// <summary>Removes <see cref="DevController"/> from MVC unless the simulator is enabled (so /api/dev/* is 404).</summary>
public sealed class DevControllerFeatureProvider : IApplicationFeatureProvider<ControllerFeature>
{
    private readonly bool _enabled;
    public DevControllerFeatureProvider(bool enabled) => _enabled = enabled;

    public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
    {
        if (_enabled) return;
        foreach (var t in feature.Controllers.Where(t => t.AsType() == typeof(DevController)).ToList())
            feature.Controllers.Remove(t);
    }
}

public sealed record SimulateGoal(long ScorerPlayerEntityId, long? AssistPlayerEntityId);

/// <summary>
/// ExtraPlayerEntityIds: additional players who "played" (0 goals). LineupPlayerEntityIds: when given, EXACTLY these
/// players played (use it to simulate that a registered player did not play).
/// </summary>
public sealed record SimulateMatchRequest(
    long ClubId, long OpponentClubId, int? MinutesFromNow, List<SimulateGoal>? OurGoals,
    int? OpponentGoals, List<long>? ExtraPlayerEntityIds, List<long>? LineupPlayerEntityIds);

public sealed class DevMatchSimulator
{
    private readonly EAFCContext _db;
    private readonly IGoalRegistrationLinker _linker;

    public DevMatchSimulator(EAFCContext db, IGoalRegistrationLinker linker)
    {
        _db = db;
        _linker = linker;
    }

    public async Task<(long MatchId, GoalLinkRunResult LinkerResult)> SimulateAsync(SimulateMatchRequest req, CancellationToken ct)
    {
        var goals = req.OurGoals ?? new();
        var ts = DateTime.UtcNow.AddMinutes(req.MinutesFromNow ?? 20);
        var matchId = 9_000_000_000_000L + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 1_000_000_000L;

        var oppName = await _db.MatchClubs.AsNoTracking()
            .Where(c => c.ClubId == req.OpponentClubId && c.Details != null && c.Details.Name != null)
            .Select(c => c.Details!.Name!).FirstOrDefaultAsync(ct)
            ?? DemoDataSeeder.Opponents.FirstOrDefault(o => o.Id == req.OpponentClubId).Name
            ?? $"Clube {req.OpponentClubId}";
        var ourName = await _db.MatchClubs.AsNoTracking()
            .Where(c => c.ClubId == req.ClubId && c.Details != null && c.Details.Name != null)
            .Select(c => c.Details!.Name!).FirstOrDefaultAsync(ct) ?? "Nosso Clube (demo)";

        var involved = req.LineupPlayerEntityIds is { Count: > 0 }
            ? req.LineupPlayerEntityIds.ToHashSet()
            : goals.SelectMany(g => new[] { (long?)g.ScorerPlayerEntityId, g.AssistPlayerEntityId })
                .Where(x => x.HasValue).Select(x => x!.Value)
                .Concat(req.ExtraPlayerEntityIds ?? new()).ToHashSet();

        var players = await _db.Players.Where(p => p.ClubId == req.ClubId && involved.Contains(p.Id)).ToListAsync(ct);
        var missing = involved.Except(players.Select(p => p.Id)).ToList();
        if (missing.Count > 0) throw new ArgumentException($"Jogadores inexistentes no clube: {string.Join(",", missing)}");
        var stats = await _db.PlayerMatchStats.Where(s => involved.Contains(s.PlayerEntityId)).ToListAsync(ct);

        var rows = players.Select(p => (p,
            goals.Count(g => g.ScorerPlayerEntityId == p.Id),
            goals.Count(g => g.AssistPlayerEntityId == p.Id))).ToList();

        var versionId = await _db.GameVersions.Where(v => v.IsCurrent).Select(v => (int?)v.Id).FirstOrDefaultAsync(ct);
        DemoMatch.Insert(_db, matchId, ts, versionId, req.ClubId, ourName, goals.Count,
            req.OpponentClubId, oppName, req.OpponentGoals ?? 1, rows, stats);
        await _db.SaveChangesAsync(ct);

        // Same entry point the fetch cycle uses at its end (FetchCoordinator.LinkGoalRegistrationsAsync).
        var result = await _linker.RunAsync(ct: ct);
        return (matchId, result);
    }
}

/// <summary>POST /api/dev/simulate-match. Only exists when the simulator is enabled (see file header).</summary>
[ApiController]
[Route("api/dev")]
public sealed class DevController : ControllerBase
{
    private readonly DevMatchSimulator _simulator;
    public DevController(DevMatchSimulator simulator) => _simulator = simulator;

    [HttpPost("simulate-match")]
    public async Task<IActionResult> SimulateMatch([FromBody] SimulateMatchRequest request, CancellationToken ct)
    {
        try
        {
            var (matchId, linkerResult) = await _simulator.SimulateAsync(request, ct);
            return Ok(new { matchId, linkerResult });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
