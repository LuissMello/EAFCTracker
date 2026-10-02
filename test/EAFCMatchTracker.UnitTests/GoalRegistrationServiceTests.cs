using EAFCMatchTracker.Application.Dtos;
using EAFCMatchTracker.Application.Exceptions;
using EAFCMatchTracker.Application.Services;
using EAFCMatchTracker.Domain.Entities;
using EAFCMatchTracker.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static EAFCMatchTracker.UnitTests.GoalTestSupport;

namespace EAFCMatchTracker.UnitTests;

public class GoalRegistrationServiceTests
{
    private static (EAFCContext db, FakeTimeProvider time, GoalRegistrationService svc) Setup()
    {
        var db = NewDb();
        var time = new FakeTimeProvider(new DateTimeOffset(T0, TimeSpan.Zero));
        AddPlayer(db, 1, Club, "Alfa");
        AddPlayer(db, 2, Club, "Beta");
        AddPlayer(db, 3, Club, "Gama");
        AddPlayer(db, 50, Opponent, "Rival"); // jogador de OUTRO clube
        db.SaveChanges();
        return (db, time, NewService(db, time));
    }

    private static CreateGoalRegistrationRequest Req(params GoalRegistrationDto[] goals) => new()
    {
        ClubId = Club,
        OpponentClubId = Opponent,
        OpponentName = "Adversário FC",
        Goals = goals.ToList()
    };

    private static GoalRegistrationDto G(long scorer, long? assist = null, long? pre = null) => new()
    {
        ScorerPlayerEntityId = scorer,
        AssistPlayerEntityId = assist,
        PreAssistPlayerEntityId = pre
    };

    // ───────────── validação da criação ─────────────

    [Fact]
    public async Task AssistEqualToScorerIsRejected()
    {
        var (_, _, svc) = Setup();
        var ex = await Assert.ThrowsAsync<DomainValidationException>(() => svc.CreateAsync(Req(G(1, 1)), default));
        Assert.Equal("O assistente deve ser diferente do artilheiro.", ex.Message);
    }

    [Fact]
    public async Task PreAssistWithoutAssistIsRejected()
    {
        var (_, _, svc) = Setup();
        var ex = await Assert.ThrowsAsync<DomainValidationException>(() => svc.CreateAsync(Req(G(1, null, 2)), default));
        Assert.Equal("A pré-assistência exige uma assistência.", ex.Message);
    }

    [Fact]
    public async Task PreAssistEqualToScorerOrAssistIsRejected()
    {
        var (_, _, svc) = Setup();
        await Assert.ThrowsAsync<DomainValidationException>(() => svc.CreateAsync(Req(G(1, 2, 1)), default));
        await Assert.ThrowsAsync<DomainValidationException>(() => svc.CreateAsync(Req(G(1, 2, 2)), default));
    }

    [Fact]
    public async Task PlayerFromAnotherClubOrUnknownIsRejected()
    {
        var (_, _, svc) = Setup();
        var other = await Assert.ThrowsAsync<DomainValidationException>(() => svc.CreateAsync(Req(G(50)), default));
        Assert.Contains("Rival", other.Message);
        Assert.Contains("não pertence ao clube", other.Message);

        var unknown = await Assert.ThrowsAsync<DomainValidationException>(() => svc.CreateAsync(Req(G(1, 999)), default));
        Assert.Contains("999", unknown.Message);
        Assert.Contains("não encontrado", unknown.Message);
    }

    [Fact]
    public async Task HeaderValidation()
    {
        var (_, _, svc) = Setup();

        var self = Req(); self.OpponentClubId = Club;
        Assert.Equal("O adversário não pode ser o próprio clube.",
            (await Assert.ThrowsAsync<DomainValidationException>(() => svc.CreateAsync(self, default))).Message);

        var noName = Req(); noName.OpponentName = "  ";
        Assert.Equal("Informe o nome do adversário.",
            (await Assert.ThrowsAsync<DomainValidationException>(() => svc.CreateAsync(noName, default))).Message);

        var longName = Req(); longName.OpponentName = new string('x', 101);
        Assert.Contains("100", (await Assert.ThrowsAsync<DomainValidationException>(() => svc.CreateAsync(longName, default))).Message);

        var longNotes = Req(); longNotes.Notes = new string('x', 501);
        Assert.Contains("500", (await Assert.ThrowsAsync<DomainValidationException>(() => svc.CreateAsync(longNotes, default))).Message);
    }

    [Fact]
    public async Task MoreThanFortyGoalsOrTooManyPerPlayerAreRejected()
    {
        var (_, _, svc) = Setup();
        var tooMany = Req(Enumerable.Range(0, 41).Select(i => G(1 + i % 3)).ToArray());
        Assert.Contains("40", (await Assert.ThrowsAsync<DomainValidationException>(() => svc.CreateAsync(tooMany, default))).Message);

        var perPlayer = Req(Enumerable.Range(0, 21).Select(_ => G(1)).ToArray());
        Assert.Contains("20", (await Assert.ThrowsAsync<DomainValidationException>(() => svc.CreateAsync(perPlayer, default))).Message);
    }

    [Fact]
    public async Task CreatingWithoutGoalsIsValidAndStartsPending()
    {
        var (_, time, svc) = Setup();

        var dto = await svc.CreateAsync(new CreateGoalRegistrationRequest { ClubId = Club, OpponentClubId = Opponent, OpponentName = " Adversário FC " }, default);

        Assert.Equal("Pending", dto.Status);
        Assert.Equal(0, dto.GoalsCount);
        Assert.Empty(dto.Goals);
        Assert.Equal("Adversário FC", dto.OpponentName);
        Assert.Null(dto.MatchId);
        Assert.Equal(time.Now.UtcDateTime, dto.CreatedAt);
        Assert.Equal(dto.CreatedAt, dto.StartedAt);
    }

    [Fact]
    public async Task CreatingWhenTheMatchAlreadyExistsReturnsLinked()
    {
        var (db, _, svc) = Setup();
        AddMatch(db, 7, T0, 1, 0, new Mp(1, 1), new Mp(2, 0, 1));
        await db.SaveChangesAsync();

        var dto = await svc.CreateAsync(Req(G(1, 2)), default);

        Assert.Equal("Linked", dto.Status);
        Assert.Equal(7, dto.MatchId);
        var goal = Assert.Single(dto.Goals);
        Assert.Equal("Jogador 1", goal.ScorerName); // ProName da partida tem prioridade sobre Playername
        Assert.Equal("Jogador 2", goal.AssistName);
        Assert.Equal(1, goal.Order);
    }

    // ───────────── edição gol a gol ─────────────

    [Fact]
    public async Task GoalsCanBeAddedEditedAndRemovedWhilePending()
    {
        var (_, _, svc) = Setup();
        var reg = await svc.CreateAsync(Req(), default);

        reg = await svc.AddGoalAsync(reg.Id, G(1, 2), default);
        reg = await svc.AddGoalAsync(reg.Id, G(2), default);
        reg = await svc.AddGoalAsync(reg.Id, G(3, 1, 2), default);
        Assert.Equal(new[] { 1, 2, 3 }, reg.Goals.Select(g => g.Order));
        Assert.Equal(3, reg.GoalsCount);

        reg = await svc.UpdateGoalAsync(reg.Id, reg.Goals[1].Id, G(3), default);
        Assert.Equal(3, reg.Goals[1].ScorerPlayerEntityId);
        Assert.Null(reg.Goals[1].AssistPlayerEntityId);

        reg = await svc.DeleteGoalAsync(reg.Id, reg.Goals[0].Id, default);
        Assert.Equal(2, reg.GoalsCount);
        Assert.Equal(new[] { 1, 2 }, reg.Goals.Select(g => g.Order)); // renumerado, ordem relativa mantida
        Assert.Equal(3, reg.Goals[0].ScorerPlayerEntityId);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => svc.DeleteGoalAsync(reg.Id, 12345, default));
        await Assert.ThrowsAsync<DomainValidationException>(() => svc.AddGoalAsync(reg.Id, G(1, 1), default));
    }

    [Fact]
    public async Task LinkedRegistrationRejectsAnInvalidEditAndChangesNothing()
    {
        var (db, _, svc) = Setup();
        AddMatch(db, 7, T0, 2, 0, new Mp(1, 1), new Mp(2, 1, 1));
        await db.SaveChangesAsync();
        var reg = await svc.CreateAsync(Req(G(1, 2)), default);
        Assert.Equal("Linked", reg.Status);

        // Gama (3) não jogou a partida
        var ex = await Assert.ThrowsAsync<DomainValidationException>(() => svc.AddGoalAsync(reg.Id, G(3), default));
        Assert.Contains("Gama", ex.Message);
        // Alfa só fez 1 gol na EA
        var ex2 = await Assert.ThrowsAsync<DomainValidationException>(() => svc.AddGoalAsync(reg.Id, G(1), default));
        Assert.Contains("Jogador 1", ex2.Message);

        var after = (await svc.GetAsync(reg.Id, default))!;
        Assert.Equal(1, after.GoalsCount);
        Assert.Equal("Linked", after.Status);
        Assert.Single(await db.MatchGoalLinks.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task LinkedRegistrationReMaterializesTheLinksOnValidEdits()
    {
        var (db, _, svc) = Setup();
        AddMatch(db, 7, T0, 2, 0, new Mp(1, 1), new Mp(2, 1, 1));
        await db.SaveChangesAsync();
        var reg = await svc.CreateAsync(Req(G(1, 2)), default);
        Assert.Equal("Linked", reg.Status);
        Assert.NotNull(reg.ReviewNote); // 1 de 2 gols: nota suave

        reg = await svc.AddGoalAsync(reg.Id, G(2), default);
        Assert.Equal("Linked", reg.Status);
        Assert.Null(reg.ReviewNote);
        var links = await db.MatchGoalLinks.AsNoTracking().OrderBy(l => l.Id).ToListAsync();
        Assert.Equal(new long[] { 1, 2 }, links.Select(l => l.ScorerPlayerEntityId));
        Assert.All(links, l => Assert.Equal(reg.Id, l.GoalRegistrationId));

        reg = await svc.DeleteGoalAsync(reg.Id, reg.Goals[0].Id, default);
        Assert.Equal("Linked", reg.Status);
        Assert.Equal(new long[] { 2 }, (await db.MatchGoalLinks.AsNoTracking().ToListAsync()).Select(l => l.ScorerPlayerEntityId));
    }

    [Fact]
    public async Task NeedsReviewRegistrationFlipsToLinkedAfterAValidEdit()
    {
        var (db, _, svc) = Setup();
        AddMatch(db, 1, T0, 1, 0, new Mp(1, 1));
        AddMatch(db, 2, T0.AddMinutes(20), 1, 0, new Mp(1, 1));
        await db.SaveChangesAsync();
        var reg = await svc.CreateAsync(Req(G(1)), default);
        Assert.Equal("NeedsReview", reg.Status); // duas candidatas
        Assert.Equal(1, reg.MatchId);

        reg = await svc.UpdateGoalAsync(reg.Id, reg.Goals[0].Id, G(1), default);

        Assert.Equal("Linked", reg.Status);
        Assert.Null(reg.ReviewNote);
        Assert.Single(await db.MatchGoalLinks.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task ExpiredRegistrationCannotBeEdited()
    {
        var (db, _, svc) = Setup();
        var reg = AddRegistration(db, T0, (1, null, null));
        reg.Status = GoalRegistrationStatus.Expired;
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<DomainConflictException>(() => svc.AddGoalAsync(reg.Id, G(2), default));
        await Assert.ThrowsAsync<DomainConflictException>(() => svc.UpdateAsync(reg.Id, new UpdateGoalRegistrationRequest { Notes = "x" }, default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => svc.AddGoalAsync(999, G(2), default));
    }

    // ───────────── PUT / DELETE / listagens ─────────────

    [Fact]
    public async Task PutReplacesTheWholeGoalListAndNotes()
    {
        var (_, _, svc) = Setup();
        var reg = await svc.CreateAsync(Req(G(1), G(2)), default);

        var updated = await svc.UpdateAsync(reg.Id, new UpdateGoalRegistrationRequest
        {
            Notes = "  jogo difícil ",
            Goals = new List<GoalRegistrationDto> { G(3, 2) }
        }, default);

        Assert.Equal("jogo difícil", updated.Notes);
        var goal = Assert.Single(updated.Goals);
        Assert.Equal((3L, 2L), (goal.ScorerPlayerEntityId, goal.AssistPlayerEntityId!.Value));
    }

    [Fact]
    public async Task ChangingTheOpponentOfALinkedRegistrationIsAConflict()
    {
        var (db, _, svc) = Setup();
        AddMatch(db, 7, T0, 1, 0, new Mp(1, 1));
        await db.SaveChangesAsync();
        var reg = await svc.CreateAsync(Req(G(1)), default);
        Assert.Equal("Linked", reg.Status);

        await Assert.ThrowsAsync<DomainConflictException>(() => svc.UpdateAsync(reg.Id,
            new UpdateGoalRegistrationRequest { OpponentClubId = 300, OpponentName = "Outro" }, default));
    }

    [Fact]
    public async Task ChangingTheOpponentOfANeedsReviewRegistrationReleasesTheMatch()
    {
        var (db, _, svc) = Setup();
        AddMatch(db, 1, T0, 1, 0, new Mp(1, 1));
        AddMatch(db, 2, T0.AddMinutes(20), 1, 0, new Mp(1, 1));
        await db.SaveChangesAsync();
        var reg = await svc.CreateAsync(Req(G(1)), default);
        Assert.Equal("NeedsReview", reg.Status);

        var updated = await svc.UpdateAsync(reg.Id,
            new UpdateGoalRegistrationRequest { OpponentClubId = 300, OpponentName = "Outro FC" }, default);

        Assert.Equal("Pending", updated.Status);
        Assert.Null(updated.MatchId);
        Assert.Equal(300, updated.OpponentClubId);
        Assert.All(await db.Matches.AsNoTracking().ToListAsync(), m => Assert.Null(m.GoalRegistrationId));
    }

    [Fact]
    public async Task DeleteRules()
    {
        var (db, _, svc) = Setup();
        var pending = await svc.CreateAsync(Req(G(1)), default);
        await svc.DeleteAsync(pending.Id, default);
        Assert.Null(await svc.GetAsync(pending.Id, default));
        Assert.Empty(await db.GoalRegistrationGoals.ToListAsync());

        AddMatch(db, 7, T0, 1, 0, new Mp(1, 1));
        await db.SaveChangesAsync();
        var linked = await svc.CreateAsync(Req(G(1)), default);
        Assert.Equal("Linked", linked.Status);
        var ex = await Assert.ThrowsAsync<DomainConflictException>(() => svc.DeleteAsync(linked.Id, default));
        Assert.Equal("Registro já vinculado a uma partida; peça a um administrador.", ex.Message);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => svc.DeleteAsync(404, default));
    }

    [Fact]
    public async Task DeletingANeedsReviewRegistrationFreesItsMatch()
    {
        var (db, _, svc) = Setup();
        AddMatch(db, 1, T0, 1, 0, new Mp(1, 1));
        AddMatch(db, 2, T0.AddMinutes(20), 1, 0, new Mp(1, 1));
        await db.SaveChangesAsync();
        var reg = await svc.CreateAsync(Req(G(1)), default);
        Assert.Equal("NeedsReview", reg.Status);

        await svc.DeleteAsync(reg.Id, default);

        Assert.Empty(await db.GoalRegistrations.ToListAsync());
        Assert.All(await db.Matches.AsNoTracking().ToListAsync(), m => Assert.Null(m.GoalRegistrationId));
    }

    [Fact]
    public async Task CurrentReturnsTheLatestOpenRegistrationFromTheLast12Hours()
    {
        var (db, time, svc) = Setup();
        Assert.Null(await svc.GetCurrentAsync(Club, default));

        var first = await svc.CreateAsync(Req(), default);
        time.Now = time.Now.AddMinutes(30);
        var second = await svc.CreateAsync(Req(G(1)), default);

        var current = await svc.GetCurrentAsync(Club, default);
        Assert.Equal(second.Id, current!.Id);

        // 13 horas depois: nada em andamento
        time.Now = time.Now.AddHours(13);
        Assert.Null(await svc.GetCurrentAsync(Club, default));
        Assert.Null(await svc.GetCurrentAsync(999, default));
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(2, await db.GoalRegistrations.CountAsync());
    }

    [Fact]
    public async Task ListDefaultsToOpenAndRecentLinkedAndFiltersByStatus()
    {
        var (db, time, svc) = Setup();
        var a = AddRegistration(db, T0);
        var b = AddRegistration(db, T0.AddMinutes(10));
        b.Status = GoalRegistrationStatus.Expired;
        var c = AddRegistration(db, T0.AddDays(-9)); // antigo demais para a listagem padrão
        await db.SaveChangesAsync();

        var list = await svc.ListAsync(Club, null, null, default);
        Assert.Equal(new[] { a.Id }, list.Select(r => r.Id));

        var expired = await svc.ListAsync(Club, "expired", null, default);
        Assert.Equal(new[] { b.Id }, expired.Select(r => r.Id));

        await Assert.ThrowsAsync<DomainValidationException>(() => svc.ListAsync(Club, "bogus", null, default));
        await Assert.ThrowsAsync<DomainValidationException>(() => svc.ListAsync(Club, "2", null, default));
        Assert.NotEqual(0, c.CreatedAt.Ticks);
        Assert.True(time.Now > DateTimeOffset.MinValue);
    }

    // ───────────── elenco ─────────────

    [Fact]
    public async Task RosterOrdersActiveFirstThenLastPlayedThenName()
    {
        var (db, time, svc) = Setup();
        var now = time.Now.UtcDateTime;
        AddMatch(db, 1, now.AddDays(-200), 1, 0, new Mp(3, 0));                           // Gama: inativo
        AddMatch(db, 2, now.AddDays(-5), 1, 0, new Mp(1, 0), new Mp(2, 0));               // Alfa/Beta: ativos
        AddMatch(db, 3, now.AddDays(-1), 1, 0, new Mp(2, 0));                             // Beta jogou mais recente
        await db.SaveChangesAsync();

        var roster = await svc.GetRosterAsync(Club, default);

        Assert.Equal(Club, roster.ClubId);
        Assert.Equal(new long[] { 2, 1, 3 }, roster.Players.Select(p => p.PlayerEntityId));
        Assert.Equal(new[] { true, true, false }, roster.Players.Select(p => p.Active));
        Assert.Equal(2, roster.Players[0].MatchesPlayed);
        Assert.Equal("Jogador 2", roster.Players[0].Name); // ProName tem prioridade
        Assert.Equal("forward", roster.Players[0].Position);
        await Assert.ThrowsAsync<DomainValidationException>(() => svc.GetRosterAsync(0, default));
    }
}
