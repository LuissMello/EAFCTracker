using EAFCMatchTracker.Application.Dtos;
using EAFCMatchTracker.Application.Exceptions;
using EAFCMatchTracker.Application.Interfaces.Services;
using EAFCMatchTracker.Application.Services;
using EAFCMatchTracker.Domain.Entities;
using EAFCMatchTracker.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Xunit;
using static EAFCMatchTracker.UnitTests.GoalTestSupport;

namespace EAFCMatchTracker.UnitTests;

/// <summary>Finalizar/reabrir, "current", sugestão de partida (adversário errado), confirmar/recusar e campos extras da busca.</summary>
public class GoalRegistrationSuggestionTests
{
    private const long OtherOpponent = 300;

    private static (EAFCContext db, FakeTimeProvider time, GoalRegistrationService svc, GoalRegistrationLinker linker) Setup(DateTime? now = null)
    {
        var db = NewDb();
        var time = new FakeTimeProvider(new DateTimeOffset(now ?? T0.AddMinutes(40), TimeSpan.Zero));
        AddPlayer(db, 1, Club, "Alfa");
        AddPlayer(db, 2, Club, "Beta");
        AddPlayer(db, 3, Club, "Gama");
        return (db, time, NewService(db, time), NewLinker(db, time));
    }

    /// <summary>Partida contra um adversário DIFERENTE do padrão de <see cref="GoalTestSupport.AddMatch"/> (id e nome reais).</summary>
    private static MatchEntity AddMatchVs(
        EAFCContext db, long matchId, DateTime ts, short ours, short theirs, long opponentId, string opponentName, params Mp[] players)
    {
        var m = AddMatch(db, matchId, ts, ours, theirs, players);
        var other = m.Clubs.Single(c => c.ClubId == Opponent);
        other.ClubId = opponentId;
        other.Details!.ClubId = opponentId;
        other.Details.Name = opponentName;
        return m;
    }

    /// <summary>O caso real: registro de 2 gols contra o clube errado (200); a partida foi contra o 300 e nosso clube fez 2.</summary>
    private static async Task<(GoalRegistrationEntity reg, MatchEntity match)> RealCase(EAFCContext db)
    {
        var match = AddMatchVs(db, 1, T0.AddMinutes(5), 2, 1, OtherOpponent, "Cantareira EC", new Mp(1, 1), new Mp(2, 1));
        var reg = AddRegistration(db, T0, (1, null, null), (2, null, null));
        reg.OpponentName = "Cantareira FC";
        await db.SaveChangesAsync();
        return (reg, match);
    }

    private static Task<GoalRegistrationEntity> Fresh(EAFCContext db, long id) =>
        db.GoalRegistrations.AsNoTracking().Include(r => r.Goals).SingleAsync(r => r.Id == id);

    // ------------------------------------------------------------------ sugestão

    [Fact]
    public async Task RealCaseGetsASuggestionAndNeverAutoLinks()
    {
        var (db, _, svc, linker) = Setup();
        var (reg, _) = await RealCase(db);

        var result = await linker.RunAsync();

        Assert.Equal((0, 1), (result.Linked, result.NeedsReview));
        var saved = await Fresh(db, reg.Id);
        Assert.Equal(GoalRegistrationStatus.NeedsReview, saved.Status);
        Assert.Equal(1, saved.SuggestedMatchId);
        Assert.Null(saved.MatchId);
        Assert.Null(saved.LinkedAt);
        Assert.Contains("Cantareira EC", saved.ReviewNote);
        Assert.Equal(OpponentOf(reg), saved.OpponentClubId); // o adversário informado não é trocado sem confirmação
        Assert.Null((await db.Matches.AsNoTracking().SingleAsync()).GoalRegistrationId);
        Assert.Empty(await db.MatchGoalLinks.ToListAsync());

        var dto = (await svc.GetAsync(reg.Id, default))!;
        Assert.Equal("NeedsReview", dto.Status);
        var s = dto.SuggestedMatch!;
        Assert.Equal((1L, OtherOpponent, "Cantareira EC", 2, 1, true), (s.MatchId, s.OpponentClubId, s.OpponentName, s.OurGoals, s.TheirGoals, s.GoalsMatch));
        Assert.Equal(T0.AddMinutes(5), s.PlayedAt);

        // rodar de novo não mexe em quem já tem sugestão
        var again = await linker.RunAsync();
        Assert.Equal((0, 0), (again.Linked, again.NeedsReview));
    }

    private static long OpponentOf(GoalRegistrationEntity reg) => Opponent;

    [Fact]
    public async Task UnfinishedRegistrationWaitsTwentyMinutesButFinishingAdvancesIt()
    {
        var (db, time, svc, linker) = Setup(T0.AddMinutes(10));
        var (reg, _) = await RealCase(db);

        await linker.RunAsync();
        Assert.Null((await Fresh(db, reg.Id)).SuggestedMatchId); // criado há 10 min e não finalizado: ainda cedo

        var finished = await svc.FinishAsync(reg.Id, default);   // finalizar roda o linker
        Assert.NotNull(finished.FinishedAt);
        Assert.Equal(1, finished.SuggestedMatch!.MatchId);
        Assert.Equal("NeedsReview", finished.Status);

        // e, sem finalizar, passados os 20 minutos
        var (db2, _, _, linker2) = Setup(T0.AddMinutes(21));
        var (reg2, _) = await RealCase(db2);
        await linker2.RunAsync();
        Assert.Equal(1, (await Fresh(db2, reg2.Id)).SuggestedMatchId);
    }

    [Fact]
    public async Task NoCandidateOrMoreThanOneCandidateSuggestsNothing()
    {
        var (db, _, _, linker) = Setup();
        var reg = AddRegistration(db, T0, (1, null, null), (2, null, null));
        await db.SaveChangesAsync();
        await linker.RunAsync(); // zero candidatas
        Assert.Null((await Fresh(db, reg.Id)).SuggestedMatchId);

        AddMatchVs(db, 1, T0.AddMinutes(5), 2, 0, 300, "Y", new Mp(1, 1), new Mp(2, 1));
        AddMatchVs(db, 2, T0.AddMinutes(30), 2, 0, 301, "Z", new Mp(1, 1), new Mp(2, 1));
        await db.SaveChangesAsync();
        await linker.RunAsync(); // duas candidatas com 2 gols nossos: ambíguo
        var saved = await Fresh(db, reg.Id);
        Assert.Equal((GoalRegistrationStatus.Pending, (long?)null), (saved.Status, saved.SuggestedMatchId));
    }

    [Fact]
    public async Task GoalCountMismatchCompetingRegistrationOrTakenMatchSuggestNothing()
    {
        // nº de gols diferente
        var (db, _, _, linker) = Setup();
        AddMatchVs(db, 1, T0.AddMinutes(5), 3, 1, 300, "Y", new Mp(1, 2), new Mp(2, 1));
        var reg = AddRegistration(db, T0, (1, null, null), (2, null, null));
        await db.SaveChangesAsync();
        await linker.RunAsync();
        Assert.Null((await Fresh(db, reg.Id)).SuggestedMatchId);

        // outro registro Pending do clube disputa a mesma partida (mesmo nº de gols, na janela)
        var (db2, _, _, linker2) = Setup();
        AddMatchVs(db2, 1, T0.AddMinutes(5), 2, 0, 300, "Y", new Mp(1, 1), new Mp(2, 1));
        var a = AddRegistration(db2, T0, (1, null, null), (2, null, null));
        var b = AddRegistration(db2, T0.AddMinutes(1), (2, null, null), (1, null, null));
        b.OpponentClubId = 555; b.OpponentName = "Outro";
        await db2.SaveChangesAsync();
        await linker2.RunAsync();
        Assert.Null((await Fresh(db2, a.Id)).SuggestedMatchId);
        Assert.Null((await Fresh(db2, b.Id)).SuggestedMatchId);

        // partida já vinculada a outro registro: excluída
        var (db3, _, _, linker3) = Setup();
        var taken = AddMatchVs(db3, 1, T0.AddMinutes(5), 2, 0, 300, "Y", new Mp(1, 1), new Mp(2, 1));
        var owner = AddRegistration(db3, T0.AddMinutes(-600), (1, null, null));
        owner.Status = GoalRegistrationStatus.Linked;
        await db3.SaveChangesAsync();
        taken.GoalRegistrationId = owner.Id;
        owner.MatchId = taken.MatchId;
        var wrong = AddRegistration(db3, T0, (1, null, null), (2, null, null));
        await db3.SaveChangesAsync();
        await linker3.RunAsync();
        Assert.Null((await Fresh(db3, wrong.Id)).SuggestedMatchId);
    }

    [Fact]
    public async Task SameOpponentMatchStillLinksNormallyAndIgnoresSuggestionRules()
    {
        var (db, _, _, linker) = Setup();
        AddMatch(db, 1, T0.AddMinutes(5), 2, 0, new Mp(1, 1), new Mp(2, 1)); // contra o adversário informado
        AddMatchVs(db, 2, T0.AddMinutes(8), 2, 0, 300, "Y", new Mp(1, 1), new Mp(2, 1));
        var reg = AddRegistration(db, T0, (1, null, null), (2, null, null));
        await db.SaveChangesAsync();

        var result = await linker.RunAsync();

        Assert.Equal(1, result.Linked);
        var saved = await Fresh(db, reg.Id);
        Assert.Equal((GoalRegistrationStatus.Linked, (long?)1, (long?)null), (saved.Status, saved.MatchId, saved.SuggestedMatchId));
    }

    // ------------------------------------------------------------------ confirmar / recusar

    [Fact]
    public async Task ConfirmSuggestionAdoptsTheRealOpponentAndLinks()
    {
        var (db, _, svc, linker) = Setup();
        var (reg, _) = await RealCase(db);
        await linker.RunAsync();

        var dto = await svc.ConfirmSuggestionAsync(reg.Id, default);

        Assert.Equal(("Linked", 1L, OtherOpponent, "Cantareira EC"), (dto.Status, dto.MatchId, dto.OpponentClubId, dto.OpponentName));
        Assert.Null(dto.SuggestedMatch);
        var saved = await Fresh(db, reg.Id);
        Assert.Equal((OtherOpponent, (long?)null), (saved.OpponentClubId, saved.SuggestedMatchId));
        Assert.NotNull(saved.LinkedAt);
        Assert.Equal(reg.Id, (await db.Matches.AsNoTracking().SingleAsync()).GoalRegistrationId);
        Assert.Equal(2, await db.MatchGoalLinks.CountAsync(l => l.GoalRegistrationId == reg.Id));

        // sem sugestão (já vinculado): conflito
        await Assert.ThrowsAsync<DomainConflictException>(() => svc.ConfirmSuggestionAsync(reg.Id, default));
    }

    [Fact]
    public async Task ConfirmSuggestionWithGoalsThatDoNotMatchFailsWithTheFriendlyErrorAndChangesNothing()
    {
        var (db, _, svc, linker) = Setup();
        // o jogador 1 aparece com 1 gol na partida, mas o registro lhe atribui 2 (a contagem total bate: 2 = 2)
        AddMatchVs(db, 1, T0.AddMinutes(5), 2, 1, OtherOpponent, "Cantareira EC", new Mp(1, 1), new Mp(2, 1));
        var reg = AddRegistration(db, T0, (1, null, null), (1, null, null));
        await db.SaveChangesAsync();
        await linker.RunAsync();
        Assert.Equal(1, (await Fresh(db, reg.Id)).SuggestedMatchId);

        var ex = await Assert.ThrowsAsync<DomainValidationException>(() => svc.ConfirmSuggestionAsync(reg.Id, default));
        Assert.Contains("registrado com 2 gol(s), mas a EA contabilizou 1", ex.Message);

        db.ChangeTracker.Clear();
        var saved = await Fresh(db, reg.Id);
        Assert.Equal((GoalRegistrationStatus.NeedsReview, (long?)1, (long?)null, Opponent), (saved.Status, saved.SuggestedMatchId, saved.MatchId, saved.OpponentClubId));
        Assert.Null((await db.Matches.AsNoTracking().SingleAsync()).GoalRegistrationId);
    }

    [Fact]
    public async Task DismissClearsTheSuggestionBackToPendingAndItIsNotSuggestedAgain()
    {
        var (db, _, svc, linker) = Setup();
        var (reg, _) = await RealCase(db);
        await linker.RunAsync();

        var dto = await svc.DismissSuggestionAsync(reg.Id, default);
        Assert.Equal(("Pending", null), (dto.Status, dto.SuggestedMatch));
        Assert.Null(dto.ReviewNote);

        await linker.RunAsync();
        var saved = await Fresh(db, reg.Id);
        Assert.Equal((GoalRegistrationStatus.Pending, (long?)null, (long?)1), (saved.Status, saved.SuggestedMatchId, saved.DismissedMatchId));

        Assert.Equal("Pending", (await svc.DismissSuggestionAsync(reg.Id, default)).Status); // idempotente
    }

    [Fact]
    public async Task SuggestionsAreNotExpiredButDieWithTheirMatch()
    {
        var (db, time, _, linker) = Setup();
        var (reg, match) = await RealCase(db);
        await linker.RunAsync();

        time.Now = time.Now.AddDays(60); // muito além do prazo de expiração dos Pending
        await linker.RunAsync();
        Assert.Equal(GoalRegistrationStatus.NeedsReview, (await Fresh(db, reg.Id)).Status);

        // a partida sugerida some: a sugestão cai e o registro volta a Pending
        time.Now = time.Now.AddDays(-60);
        db.Matches.Remove(await db.Matches.SingleAsync());
        await db.SaveChangesAsync();
        await linker.RunAsync();
        var saved = await Fresh(db, reg.Id);
        Assert.Equal((GoalRegistrationStatus.Pending, (long?)null), (saved.Status, saved.SuggestedMatchId));
    }

    [Fact]
    public async Task EditingGoalsInvalidatesTheSuggestion()
    {
        var (db, _, svc, linker) = Setup();
        var (reg, _) = await RealCase(db);
        await linker.RunAsync();

        var dto = await svc.AddGoalAsync(reg.Id, new GoalRegistrationDto { ScorerPlayerEntityId = 3 }, default);

        Assert.Equal(("Pending", 3, null), (dto.Status, dto.GoalsCount, dto.SuggestedMatch)); // 3 gols x partida de 2: sem sugestão
    }

    // ------------------------------------------------------------------ finalizar / reabrir / current

    [Fact]
    public async Task FinishAndReopenFollowTheStateRules()
    {
        var (db, _, svc, _) = Setup(T0.AddMinutes(1));
        var reg = AddRegistration(db, T0, (1, null, null));
        var review = AddRegistration(db, T0, (1, null, null));
        review.Status = GoalRegistrationStatus.NeedsReview;
        review.MatchId = 778; // com partida (senão a higiene do linker o trataria como órfão)
        var linked = AddRegistration(db, T0, (1, null, null));
        linked.Status = GoalRegistrationStatus.Linked;
        linked.MatchId = 777;
        var expired = AddRegistration(db, T0, (1, null, null));
        expired.Status = GoalRegistrationStatus.Expired;
        await db.SaveChangesAsync();

        var first = await svc.FinishAsync(reg.Id, default);
        Assert.NotNull(first.FinishedAt);
        Assert.Equal("Pending", first.Status);
        time(db); // sem efeito: só para manter o db vivo
        var second = await svc.FinishAsync(reg.Id, default);
        Assert.Equal(first.FinishedAt, second.FinishedAt); // idempotente: não muda o instante

        Assert.NotNull((await svc.FinishAsync(review.Id, default)).FinishedAt);  // NeedsReview também finaliza
        Assert.Null((await svc.FinishAsync(linked.Id, default)).FinishedAt);     // Linked: devolvido sem alterar
        await Assert.ThrowsAsync<DomainConflictException>(() => svc.FinishAsync(expired.Id, default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => svc.FinishAsync(9999, default));

        var reopened = await svc.ReopenAsync(reg.Id, default);
        Assert.Null(reopened.FinishedAt);
        Assert.Null((await svc.ReopenAsync(reg.Id, default)).FinishedAt);         // idempotente
        await Assert.ThrowsAsync<DomainConflictException>(() => svc.ReopenAsync(review.Id, default)); // só Pending
        await Assert.ThrowsAsync<DomainConflictException>(() => svc.ReopenAsync(linked.Id, default));
    }

    private static void time(EAFCContext _) { }

    [Fact]
    public async Task CurrentOnlyReturnsTheRegistrationStillInProgress()
    {
        var (db, _, svc, _) = Setup(T0.AddMinutes(30));
        Assert.Null(await svc.GetCurrentAsync(Club, default));

        var reg = AddRegistration(db, T0, (1, null, null));
        await db.SaveChangesAsync();
        Assert.Equal(reg.Id, (await svc.GetCurrentAsync(Club, default))!.Id);

        await svc.FinishAsync(reg.Id, default);
        Assert.Null(await svc.GetCurrentAsync(Club, default)); // finalizado: a tela de gols pode começar um novo

        await svc.ReopenAsync(reg.Id, default);
        Assert.Equal(reg.Id, (await svc.GetCurrentAsync(Club, default))!.Id);

        var next = AddRegistration(db, T0.AddMinutes(20), (2, null, null));
        next.Status = GoalRegistrationStatus.NeedsReview; // NeedsReview não é "em andamento"
        await db.SaveChangesAsync();
        Assert.Equal(reg.Id, (await svc.GetCurrentAsync(Club, default))!.Id);
    }

    // ------------------------------------------------------------------ busca de adversários

    [Fact]
    public async Task OpponentSearchExposesTeamIdDivisionAndSkillRatingWhenKnown()
    {
        var db = NewDb();
        var ea = new FakeEaClubSearch
        {
            Result = new EaSearchResult(true, new[]
            {
                new EaClubInfo(186441, "Cantareira EC", 4, 2, 3, 100, 10, 6, 2, 2, 20, 10, 1, 0, "243", null, 1873),
                new EaClubInfo(51759, "Cantareira FC", null, null, null, null, null, null, null, null, null, null, null, null, null, null, null)
            })
        };
        var svc = new OpponentSearchService(db, ea, new MemoryCache(new MemoryCacheOptions()));

        var r = await svc.SearchAsync("cantareira", Club, null, default);

        var ec = r.Results.Single(x => x.ClubId == 186441);
        Assert.Equal((243L, 4, 4, 1873, "243"), (ec.TeamId, ec.Division, ec.CurrentDivision, ec.SkillRating, ec.CrestAssetId));
        var fc = r.Results.Single(x => x.ClubId == 51759);
        Assert.Null(fc.TeamId);
        Assert.Null(fc.Division);
        Assert.Null(fc.SkillRating);
    }

    [Fact]
    public void EaSearchPayloadSkillRatingIsParsedWhenPresent()
    {
        var json = """
        [ {"clubId":"1","clubInfo":{"name":"A","teamId":243},"currentDivision":3,"skillRating":1500},
          {"clubId":"2","clubInfo":{"name":"B","teamId":10}} ]
        """;
        var list = EaClubSearchClient.Parse(json);
        Assert.Equal((1500, 3, "243"), (list[0].SkillRating, list[0].CurrentDivision, list[0].CrestAssetId));
        Assert.Null(list[1].SkillRating);
    }
    // ------------------------------------------------------------------ fixtures (só quando GOALREG_FIXTURE_DIR está definido)

    [Fact]
    public async Task WritesFixturesWhenAskedTo()
    {
        var dir = Environment.GetEnvironmentVariable("GOALREG_FIXTURE_DIR");
        if (string.IsNullOrWhiteSpace(dir)) return;
        Directory.CreateDirectory(dir);
        var json = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web) { WriteIndented = true };
        void Save(string name, object? value) => File.WriteAllText(Path.Combine(dir, name), System.Text.Json.JsonSerializer.Serialize(value, json));

        var (db, _, svc, linker) = Setup(T0.AddMinutes(10));
        var (reg, _) = await RealCase(db);
        Save("goalreg-in-progress.json", await svc.GetCurrentAsync(Club, default));

        var finished = await svc.FinishAsync(reg.Id, default);       // finalizar já gera a sugestão (adversário errado)
        Save("goalreg-finished-with-suggestion.json", finished);
        Save("goalreg-current-after-finish.json", await svc.GetCurrentAsync(Club, default)); // null => 204
        Save("goalreg-list.json", await svc.ListAsync(Club, null, null, default));
        Save("goalreg-confirmed.json", await svc.ConfirmSuggestionAsync(reg.Id, default));

        var (db2, _, svc2, _) = Setup(T0.AddMinutes(10));
        var (reg2, _) = await RealCase(db2);
        await svc2.FinishAsync(reg2.Id, default);
        Save("goalreg-dismissed.json", await svc2.DismissSuggestionAsync(reg2.Id, default));
        Save("goalreg-reopened.json", await svc2.ReopenAsync(reg2.Id, default));

        var ea = new FakeEaClubSearch
        {
            Result = new EaSearchResult(true, new[]
            {
                new EaClubInfo(186441, "Cantareira EC", 4, 2, 3, 100, 10, 6, 2, 2, 20, 10, 1, 0, "243", "7788", 1873),
                new EaClubInfo(51759, "Cantareira FC", 7, 5, 2, 40, 6, 2, 1, 3, 9, 12, 0, 1, "21", null, null)
            })
        };
        Save("goalreg-opponent-search.json",
            await new OpponentSearchService(NewDb(), ea, new MemoryCache(new MemoryCacheOptions())).SearchAsync("cantareira", Club, null, default));
    }
}
