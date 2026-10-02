using EAFCMatchTracker.Application.Services;
using EAFCMatchTracker.Domain.Entities;
using EAFCMatchTracker.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;
using static EAFCMatchTracker.UnitTests.GoalTestSupport;

namespace EAFCMatchTracker.UnitTests;

public class GoalRegistrationLinkerTests
{
    private static (EAFCContext db, FakeTimeProvider time, GoalRegistrationLinker linker) Setup(DateTime? now = null)
    {
        var db = NewDb();
        var time = new FakeTimeProvider(new DateTimeOffset(now ?? T0.AddHours(2), TimeSpan.Zero));
        AddPlayer(db, 1, Club, "Alfa");
        AddPlayer(db, 2, Club, "Beta");
        AddPlayer(db, 3, Club, "Gama");
        return (db, time, NewLinker(db, time));
    }

    [Fact]
    public async Task LinksARegistrationToTheMatchAndMaterializesGoalLinks()
    {
        var (db, _, linker) = Setup();
        // A marcou 1 (sem assistir), B marcou 1 e deu 1 assistência; C pré-assistiu
        AddMatch(db, 1, T0, 2, 1, new Mp(1, 1), new Mp(2, 1, 1), new Mp(3, 0));
        var reg = AddRegistration(db, T0.AddMinutes(10), (1, 2, 3), (2, null, null));
        await db.SaveChangesAsync();

        var result = await linker.RunAsync();

        Assert.Equal((1, 0, 0), (result.Linked, result.NeedsReview, result.Expired));
        var saved = await db.GoalRegistrations.AsNoTracking().SingleAsync();
        Assert.Equal(GoalRegistrationStatus.Linked, saved.Status);
        Assert.Equal(1, saved.MatchId);
        Assert.NotNull(saved.LinkedAt);
        Assert.Null(saved.ReviewNote);

        var match = await db.Matches.AsNoTracking().SingleAsync();
        Assert.Equal(reg.Id, match.GoalRegistrationId);

        var links = await db.MatchGoalLinks.AsNoTracking().OrderBy(l => l.Id).ToListAsync();
        Assert.Equal(2, links.Count);
        Assert.All(links, l =>
        {
            Assert.Equal(Club, l.ClubId);
            Assert.Equal(1, l.MatchId);
            Assert.Equal(reg.Id, l.GoalRegistrationId);
        });
        Assert.Equal((1L, 2L, 3L), (links[0].ScorerPlayerEntityId, links[0].AssistPlayerEntityId!.Value, links[0].PreAssistPlayerEntityId!.Value));
        Assert.Equal(2L, links[1].ScorerPlayerEntityId);

        // PreAssists recalculado a partir dos links (como RegisterGoalsAsync)
        var preAssists = await db.MatchPlayers.AsNoTracking().Where(mp => mp.PlayerEntityId == 3).Select(mp => mp.PreAssists).SingleAsync();
        Assert.Equal(1, preAssists);
    }

    [Fact]
    public async Task EmptyRegistrationAlsoLinksToTheMatch()
    {
        var (db, _, linker) = Setup();
        AddMatch(db, 1, T0, 0, 3, new Mp(1, 0));
        AddRegistration(db, T0.AddMinutes(-5));
        await db.SaveChangesAsync();

        await linker.RunAsync();

        var saved = await db.GoalRegistrations.AsNoTracking().SingleAsync();
        Assert.Equal(GoalRegistrationStatus.Linked, saved.Status);
        Assert.Equal(1, saved.MatchId);
        Assert.Null(saved.ReviewNote); // 0 gols registrados, 0 reais
        Assert.Empty(await db.MatchGoalLinks.ToListAsync());
    }

    [Fact]
    public async Task MatchOutsideTheWindowKeepsTheRegistrationPending()
    {
        var (db, _, linker) = Setup();
        AddMatch(db, 1, T0.AddHours(7), 1, 0, new Mp(1, 1)); // janela padrão = 360 min
        AddRegistration(db, T0, (1, null, null));
        await db.SaveChangesAsync();

        var result = await linker.RunAsync();

        Assert.Equal(0, result.Linked + result.NeedsReview);
        var saved = await db.GoalRegistrations.AsNoTracking().SingleAsync();
        Assert.Equal(GoalRegistrationStatus.Pending, saved.Status);
        Assert.Null(saved.MatchId);
        Assert.Null((await db.Matches.AsNoTracking().SingleAsync()).GoalRegistrationId);
        Assert.Empty(await db.MatchGoalLinks.ToListAsync());
    }

    [Fact]
    public async Task WindowSettingIsHonored()
    {
        var (db, _, linker) = Setup();
        db.AppSettings.Add(new AppSettingEntity { Key = AppSettingEntity.Keys.GoalLinkWindowMinutes, Value = "30" });
        AddMatch(db, 1, T0.AddMinutes(45), 1, 0, new Mp(1, 1));
        AddRegistration(db, T0, (1, null, null));
        await db.SaveChangesAsync();

        await linker.RunAsync();

        Assert.Equal(GoalRegistrationStatus.Pending, (await db.GoalRegistrations.AsNoTracking().SingleAsync()).Status);
    }

    [Fact]
    public async Task MatchBeforeTheRegistrationBeyondTheSlackDoesNotLinkButWithinItDoes()
    {
        var (db, _, linker) = Setup();
        AddMatch(db, 1, T0.AddMinutes(-20), 1, 0, new Mp(1, 1)); // 20 min antes: fora do slack (15)
        AddRegistration(db, T0, (1, null, null));
        await db.SaveChangesAsync();
        await linker.RunAsync();
        Assert.Equal(GoalRegistrationStatus.Pending, (await db.GoalRegistrations.AsNoTracking().SingleAsync()).Status);

        var (db2, _, linker2) = Setup();
        AddMatch(db2, 1, T0.AddMinutes(-10), 1, 0, new Mp(1, 1)); // 10 min antes: dentro do slack
        AddRegistration(db2, T0, (1, null, null));
        await db2.SaveChangesAsync();
        await linker2.RunAsync();
        Assert.Equal(GoalRegistrationStatus.Linked, (await db2.GoalRegistrations.AsNoTracking().SingleAsync()).Status);
    }

    [Fact]
    public async Task SlackIsConfigurable()
    {
        var (db, time, _) = Setup();
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["GoalRegistration:LinkSlackMinutes"] = "30" }).Build();
        var linker = new GoalRegistrationLinker(db, time, Microsoft.Extensions.Logging.Abstractions.NullLogger<GoalRegistrationLinker>.Instance, config);
        AddMatch(db, 1, T0.AddMinutes(-25), 1, 0, new Mp(1, 1));
        AddRegistration(db, T0, (1, null, null));
        await db.SaveChangesAsync();
        await linker.RunAsync();
        Assert.Equal(GoalRegistrationStatus.Linked, (await db.GoalRegistrations.AsNoTracking().SingleAsync()).Status);
    }

    [Fact]
    public async Task UnregisteredGameOneIsNotLinkedToTheLiveRegistrationOfGameTwo()
    {
        var (db, _, linker) = Setup();
        AddMatch(db, 1, T0, 1, 0, new Mp(1, 1)); // jogo 1 já importado, sem registro
        AddRegistration(db, T0.AddMinutes(40), (1, null, null)); // jogo 2 registrado ao vivo
        await db.SaveChangesAsync();

        await linker.RunAsync();

        var reg = await db.GoalRegistrations.AsNoTracking().SingleAsync();
        Assert.Equal(GoalRegistrationStatus.Pending, reg.Status);
        Assert.Null((await db.Matches.AsNoTracking().SingleAsync()).GoalRegistrationId);

        AddMatch(db, 2, T0.AddMinutes(60), 1, 0, new Mp(1, 1)); // o jogo 2 chega: agora vincula a ele
        await db.SaveChangesAsync();
        await linker.RunAsync();
        Assert.Equal(2, (await db.GoalRegistrations.AsNoTracking().SingleAsync()).MatchId);
    }

    [Fact]
    public async Task TwoLiveGamesInARowArePairedInOrder()
    {
        var (db, _, linker) = Setup();
        AddMatch(db, 1, T0.AddMinutes(20), 1, 0, new Mp(1, 1));
        AddMatch(db, 2, T0.AddMinutes(50), 1, 0, new Mp(2, 1));
        var r1 = AddRegistration(db, T0, (1, null, null));
        var r2 = AddRegistration(db, T0.AddMinutes(40), (2, null, null));
        await db.SaveChangesAsync();

        var result = await linker.RunAsync();

        Assert.Equal(2, result.Linked);
        var regs = await db.GoalRegistrations.AsNoTracking().ToDictionaryAsync(r => r.Id);
        Assert.Equal(1, regs[r1.Id].MatchId);
        Assert.Equal(2, regs[r2.Id].MatchId);
    }

    [Fact]
    public async Task ConsecutiveGamesAgainstTheSameOpponentArePairedInOrder()
    {
        var (db, _, linker) = Setup();
        AddMatch(db, 1, T0, 1, 0, new Mp(1, 1));                    // jogo 1: gol do Alfa
        AddMatch(db, 2, T0.AddMinutes(30), 1, 0, new Mp(2, 1));     // jogo 2: gol do Beta
        // criados fora de ordem de inserção para provar que a ordem é por (CreatedAt, Id)
        var r2 = AddRegistration(db, T0.AddMinutes(25), (2, null, null));
        var r1 = AddRegistration(db, T0.AddMinutes(-15), (1, null, null));
        await db.SaveChangesAsync();

        var result = await linker.RunAsync();

        Assert.Equal(2, result.Linked);
        Assert.Equal(0, result.NeedsReview);
        var regs = await db.GoalRegistrations.AsNoTracking().ToDictionaryAsync(r => r.Id);
        Assert.Equal(1, regs[r1.Id].MatchId);
        Assert.Equal(2, regs[r2.Id].MatchId);
        Assert.All(regs.Values, r => Assert.Equal(GoalRegistrationStatus.Linked, r.Status));

        var links = await db.MatchGoalLinks.AsNoTracking().ToListAsync();
        Assert.Equal(1L, links.Single(l => l.MatchId == 1).ScorerPlayerEntityId);
        Assert.Equal(2L, links.Single(l => l.MatchId == 2).ScorerPlayerEntityId);
    }

    [Fact]
    public async Task SecondGameStaysPendingUntilItsMatchArrives()
    {
        var (db, _, linker) = Setup();
        AddMatch(db, 1, T0, 1, 0, new Mp(1, 1));
        var r1 = AddRegistration(db, T0.AddMinutes(-15), (1, null, null));
        var r2 = AddRegistration(db, T0.AddMinutes(25), (2, null, null));
        await db.SaveChangesAsync();

        await linker.RunAsync();

        var regs = await db.GoalRegistrations.AsNoTracking().ToDictionaryAsync(r => r.Id);
        Assert.Equal(GoalRegistrationStatus.Linked, regs[r1.Id].Status);
        Assert.Equal(GoalRegistrationStatus.Pending, regs[r2.Id].Status);

        // a segunda partida chega no ciclo seguinte
        AddMatch(db, 2, T0.AddMinutes(30), 1, 0, new Mp(2, 1));
        await db.SaveChangesAsync();
        await linker.RunAsync();

        regs = await db.GoalRegistrations.AsNoTracking().ToDictionaryAsync(r => r.Id);
        Assert.Equal(GoalRegistrationStatus.Linked, regs[r2.Id].Status);
        Assert.Equal(2, regs[r2.Id].MatchId);
    }

    [Fact]
    public async Task OneRegistrationAndTwoCandidateMatchesNeedsReviewWithoutLinks()
    {
        var (db, _, linker) = Setup();
        AddMatch(db, 1, T0, 1, 0, new Mp(1, 1));
        AddMatch(db, 2, T0.AddMinutes(30), 1, 0, new Mp(1, 1));
        var reg = AddRegistration(db, T0.AddMinutes(5), (1, null, null));
        await db.SaveChangesAsync();

        var result = await linker.RunAsync();

        Assert.Equal(1, result.NeedsReview);
        var saved = await db.GoalRegistrations.AsNoTracking().SingleAsync();
        Assert.Equal(GoalRegistrationStatus.NeedsReview, saved.Status);
        Assert.Equal(1, saved.MatchId); // pareada na ordem: a primeira candidata
        Assert.Equal(GoalRegistrationLinker.AmbiguousNote, saved.ReviewNote);
        Assert.Null(saved.LinkedAt);
        Assert.Empty(await db.MatchGoalLinks.ToListAsync());

        var matches = await db.Matches.AsNoTracking().ToDictionaryAsync(m => m.MatchId);
        Assert.Equal(reg.Id, matches[1].GoalRegistrationId);
        Assert.Null(matches[2].GoalRegistrationId);
    }

    [Fact]
    public async Task MoreRegisteredGoalsThanTheMatchHadNeedsReview()
    {
        var (db, _, linker) = Setup();
        AddMatch(db, 1, T0, 1, 0, new Mp(1, 1));
        AddRegistration(db, T0, (1, null, null), (1, null, null)); // Alfa com 2 gols; EA diz 1
        await db.SaveChangesAsync();

        await linker.RunAsync();

        var saved = await db.GoalRegistrations.AsNoTracking().SingleAsync();
        Assert.Equal(GoalRegistrationStatus.NeedsReview, saved.Status);
        Assert.Equal(1, saved.MatchId);
        Assert.Contains("Jogador 1", saved.ReviewNote);
        Assert.Contains("2 gol(s)", saved.ReviewNote);
        Assert.Empty(await db.MatchGoalLinks.ToListAsync());
        Assert.NotNull((await db.Matches.AsNoTracking().SingleAsync()).GoalRegistrationId);
    }

    [Fact]
    public async Task RegisteredGoalsAboveTheClubTotalNeedsReview()
    {
        var (db, _, linker) = Setup();
        // dados da EA inconsistentes entre jogador e clube: o total do clube também vale
        AddMatch(db, 1, T0, 1, 0, new Mp(1, 2));
        AddRegistration(db, T0, (1, null, null), (1, null, null));
        await db.SaveChangesAsync();

        await linker.RunAsync();

        var saved = await db.GoalRegistrations.AsNoTracking().SingleAsync();
        Assert.Equal(GoalRegistrationStatus.NeedsReview, saved.Status);
        Assert.Equal("Foram registrados 2 gols, mas a partida teve apenas 1.", saved.ReviewNote);
        Assert.Empty(await db.MatchGoalLinks.ToListAsync());
    }

    [Fact]
    public async Task FewerRegisteredGoalsThanRealStillLinksWithASoftNote()
    {
        var (db, _, linker) = Setup();
        AddMatch(db, 1, T0, 3, 0, new Mp(1, 2), new Mp(2, 1));
        AddRegistration(db, T0, (1, null, null));
        await db.SaveChangesAsync();

        await linker.RunAsync();

        var saved = await db.GoalRegistrations.AsNoTracking().SingleAsync();
        Assert.Equal(GoalRegistrationStatus.Linked, saved.Status);
        Assert.Equal("Foram registrados 1 gols; a partida teve 3.", saved.ReviewNote);
        Assert.Single(await db.MatchGoalLinks.ToListAsync());
    }

    [Fact]
    public async Task PlayerWhoDidNotPlayTheMatchNeedsReview()
    {
        var (db, _, linker) = Setup();
        AddMatch(db, 1, T0, 1, 0, new Mp(1, 1)); // Beta (2) não jogou
        AddRegistration(db, T0, (2, null, null));
        await db.SaveChangesAsync();

        await linker.RunAsync();

        var saved = await db.GoalRegistrations.AsNoTracking().SingleAsync();
        Assert.Equal(GoalRegistrationStatus.NeedsReview, saved.Status);
        Assert.Contains("Beta", saved.ReviewNote);
        Assert.Empty(await db.MatchGoalLinks.ToListAsync());
    }

    [Fact]
    public async Task ExistingManualLinksAreNeverOverwritten()
    {
        var (db, _, linker) = Setup();
        AddMatch(db, 1, T0, 1, 0, new Mp(1, 1));
        db.MatchGoalLinks.Add(new MatchGoalLinkEntity { MatchId = 1, ClubId = Club, ScorerPlayerEntityId = 1 });
        AddRegistration(db, T0, (1, null, null));
        await db.SaveChangesAsync();

        await linker.RunAsync();

        var saved = await db.GoalRegistrations.AsNoTracking().SingleAsync();
        Assert.Equal(GoalRegistrationStatus.NeedsReview, saved.Status);
        Assert.Equal(GoalRegistrationLinker.ManualLinksNote, saved.ReviewNote);
        var link = await db.MatchGoalLinks.AsNoTracking().SingleAsync();
        Assert.Null(link.GoalRegistrationId); // continua o link manual original
    }

    [Fact]
    public async Task DeletedMatchSendsTheRegistrationBackToPendingAndRelinksOnReimport()
    {
        var (db, _, linker) = Setup();
        AddMatch(db, 1, T0, 1, 0, new Mp(1, 1));
        AddRegistration(db, T0, (1, null, null));
        await db.SaveChangesAsync();
        await linker.RunAsync();
        Assert.Equal(GoalRegistrationStatus.Linked, (await db.GoalRegistrations.AsNoTracking().SingleAsync()).Status);

        // MatchRepository.DeleteMatchesAsync apaga links/jogadores/clubes/partida; a FK ON DELETE SET NULL zera MatchId
        db.MatchGoalLinks.RemoveRange(db.MatchGoalLinks);
        db.Matches.RemoveRange(await db.Matches.ToListAsync());
        var reg = await db.GoalRegistrations.SingleAsync();
        reg.MatchId = null;
        await db.SaveChangesAsync();

        await linker.RunAsync();

        var back = await db.GoalRegistrations.AsNoTracking().SingleAsync();
        Assert.Equal(GoalRegistrationStatus.Pending, back.Status);
        Assert.Null(back.MatchId);
        Assert.Null(back.LinkedAt);
        Assert.Null(back.ReviewNote);

        // a partida é reimportada e o registro vincula de novo
        AddMatch(db, 1, T0, 1, 0, new Mp(1, 1));
        await db.SaveChangesAsync();
        await linker.RunAsync();
        Assert.Equal(GoalRegistrationStatus.Linked, (await db.GoalRegistrations.AsNoTracking().SingleAsync()).Status);
        Assert.Single(await db.MatchGoalLinks.ToListAsync());
    }

    [Fact]
    public async Task PendingRegistrationsExpireAfterTheConfiguredDays()
    {
        var (db, time, linker) = Setup(now: T0.AddDays(8)); // padrão = 7 dias
        var old = AddRegistration(db, T0, (1, null, null));
        var recent = AddRegistration(db, T0.AddDays(2), (1, null, null));
        await db.SaveChangesAsync();

        var result = await linker.RunAsync();

        Assert.Equal(1, result.Expired);
        var regs = await db.GoalRegistrations.AsNoTracking().ToDictionaryAsync(r => r.Id);
        Assert.Equal(GoalRegistrationStatus.Expired, regs[old.Id].Status);
        Assert.Equal(GoalRegistrationStatus.Pending, regs[recent.Id].Status);

        // configuração: 10 dias -> o antigo (8 dias) não expira
        var db2 = NewDb();
        AddPlayer(db2, 1, Club, "Alfa");
        db2.AppSettings.Add(new AppSettingEntity { Key = AppSettingEntity.Keys.GoalRegistrationExpireDays, Value = "10" });
        AddRegistration(db2, T0, (1, null, null));
        await db2.SaveChangesAsync();
        var result2 = await NewLinker(db2, time).RunAsync();
        Assert.Equal(0, result2.Expired);
    }

    [Fact]
    public async Task RunningTwiceIsIdempotent()
    {
        var (db, _, linker) = Setup();
        AddMatch(db, 1, T0, 2, 0, new Mp(1, 1), new Mp(2, 1, 1));
        AddRegistration(db, T0, (1, 2, null), (2, null, null));
        AddRegistration(db, T0.AddDays(-30), (1, null, null)); // expirado
        AddRegistration(db, T0.AddHours(1), (2, null, null));  // sem partida: continua Pending
        await db.SaveChangesAsync();

        var first = await linker.RunAsync();
        var snapshot1 = await Snapshot(db);
        var second = await linker.RunAsync();
        var snapshot2 = await Snapshot(db);

        Assert.Equal(1, first.Linked);
        Assert.Equal(1, first.Expired);
        Assert.Equal((0, 0, 0), (second.Linked, second.NeedsReview, second.Expired));
        Assert.Equal(snapshot1, snapshot2);
        Assert.Equal(2, await db.MatchGoalLinks.CountAsync());
    }

    [Fact]
    public async Task ClubAndOpponentFilterOnlyProcessesThatGroup()
    {
        var (db, _, linker) = Setup();
        AddMatch(db, 1, T0, 1, 0, new Mp(1, 1));
        AddRegistration(db, T0, (1, null, null));
        await db.SaveChangesAsync();

        var other = await linker.RunAsync(clubId: Club, opponentClubId: 999);
        Assert.Equal(0, other.Linked);
        Assert.Equal(GoalRegistrationStatus.Pending, (await db.GoalRegistrations.AsNoTracking().SingleAsync()).Status);

        var same = await linker.RunAsync(Club, Opponent);
        Assert.Equal(1, same.Linked);
    }

    private static async Task<string> Snapshot(EAFCContext db)
    {
        var regs = await db.GoalRegistrations.AsNoTracking().OrderBy(r => r.Id)
            .Select(r => $"{r.Id}:{r.Status}:{r.MatchId}:{r.ReviewNote}").ToListAsync();
        var links = await db.MatchGoalLinks.AsNoTracking().OrderBy(l => l.Id)
            .Select(l => $"{l.Id}:{l.MatchId}:{l.ScorerPlayerEntityId}:{l.GoalRegistrationId}").ToListAsync();
        return string.Join("|", regs) + "#" + string.Join("|", links);
    }
}
