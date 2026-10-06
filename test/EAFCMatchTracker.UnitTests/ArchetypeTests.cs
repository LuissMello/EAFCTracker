using EAFCMatchTracker.Api.Controllers;
using EAFCMatchTracker.Application.Dtos;
using EAFCMatchTracker.Application.Repositories;
using EAFCMatchTracker.Application.Services;
using EAFCMatchTracker.Application.Services.Analytics;
using EAFCMatchTracker.Domain.Entities;
using EAFCMatchTracker.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EAFCMatchTracker.UnitTests;

/// <summary>Arquétipos de jogador: catálogo (nomes conhecidos, rótulo padrão, inferência, resiliência), uso/trocas, resumo, cartas, perfil, noite e retrospectiva.</summary>
public class ArchetypeTests
{
    private static readonly DateTime T0 = AnalyticsSeed.Base;
    private static DateTime Day(int i) => T0.AddDays(i);

    /// <summary>11 = meia (8,8,9,0,9) · 12 = atacante (12,12,30,12; ausente na 5ª).</summary>
    private static AnalyticsSeed Seeded()
    {
        var s = new AnalyticsSeed();
        s.Match(Day(0), 2, 1, ours: new[]
        {
            new Pl(11, Goals: 1, Rating: 7, Pos: "midfielder", Arch: 8, Ovr: 80),
            new Pl(12, Goals: 2, Rating: 8, Pos: "forward", Arch: 12)
        });
        s.Match(Day(1), 1, 1, ours: new[]
        {
            new Pl(11, Assists: 1, Rating: 8, Pos: "midfielder", Arch: 8, OvrStr: "82"),
            new Pl(12, Rating: 7, Pos: "forward", Arch: 12)
        });
        s.Match(Day(2), 0, 1, ours: new[]
        {
            new Pl(11, Rating: 6, Pos: "midfielder", Arch: 9),
            new Pl(12, Rating: 6, Pos: "forward", Arch: 30)
        });
        s.Match(Day(3), 3, 0, ours: new[]
        {
            new Pl(11, Rating: 7, Pos: "midfielder", Arch: 0),
            new Pl(12, Goals: 1, Rating: 8, Pos: "forward", Arch: 12)
        });
        s.Match(Day(4), 2, 0, ours: new[] { new Pl(11, Goals: 1, Rating: 7, Pos: "midfielder", Arch: 9, Ovr: 76) });
        return s;
    }

    /// <summary>Uma noite (3 partidas em 80 min): 11 joga 8,8,9 (com 1 gol) e 12 não tem dado de arquétipo.</summary>
    private static (AnalyticsSeed Seed, long NightId) OneNight()
    {
        var s = new AnalyticsSeed();
        var m1 = s.Match(T0, 1, 0, ours: new[] { new Pl(11, Goals: 1, Pos: "midfielder", Arch: 8, Rating: 7), new Pl(12, Rating: 6) });
        s.Match(T0.AddMinutes(40), 0, 0, ours: new[] { new Pl(11, Pos: "midfielder", Arch: 8, Rating: 8), new Pl(12, Rating: 6) });
        s.Match(T0.AddMinutes(80), 0, 1, ours: new[] { new Pl(11, Pos: "midfielder", Arch: 9, Rating: 6), new Pl(12, Rating: 6) });
        return (s, m1);
    }

    private sealed class CountingLogger : ILogger<ArchetypeCatalog>
    {
        public int Warnings;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning) Warnings++;
        }
    }

    /// <summary>Simula o banco SEM a migration: toda leitura do catálogo falha (como o Postgres 42P01).</summary>
    private sealed class MissingTableCatalog : ArchetypeCatalog
    {
        public int Reads;
        public MissingTableCatalog(IServiceScopeFactory scopes, ILogger<ArchetypeCatalog> logger) : base(scopes, logger) { }
        protected override Task<List<PlayerArchetypeEntity>> ReadEntriesAsync(EAFCContext db, CancellationToken ct)
        {
            Reads++;
            throw new InvalidOperationException("relation \"PlayerArchetypes\" does not exist (42P01)");
        }
    }

    private sealed class CountingCatalog : ArchetypeCatalog
    {
        public int Reads;
        public CountingCatalog(IServiceScopeFactory scopes, ILogger<ArchetypeCatalog> logger) : base(scopes, logger) { }
        protected override Task<List<PlayerArchetypeEntity>> ReadEntriesAsync(EAFCContext db, CancellationToken ct)
        {
            Reads++;
            return base.ReadEntriesAsync(db, ct);
        }
    }

    // ------------------------------------------------------------------ catálogo: nomes conhecidos e rótulo padrão

    [Fact]
    public async Task KnownMapNamesTheThirteenArchetypesAndLeavesOthersUnnamed()
    {
        using var seed = Seeded(); // sem nenhuma linha em PlayerArchetypes: vale o padrão em código
        var snap = await seed.Catalog().GetAsync();

        var expected = new (int Id, string Name, string Group)[]
        {
            (1, "Shot Stopper", "GOLEIRO"), (2, "Sweeper Keeper", "GOLEIRO"),
            (3, "Progressor", "DEFESA"), (4, "Boss", "DEFESA"), (5, "Disruptor", "DEFESA"), (6, "Marauder", "DEFESA"),
            (7, "Recycler", "MEIO"), (8, "Maestro", "MEIO"), (9, "Creator", "MEIO"), (10, "Spark", "MEIO"),
            (11, "Magician", "ATAQUE"), (12, "Finisher", "ATAQUE"), (13, "Target", "ATAQUE")
        };
        foreach (var (id, name, group) in expected)
        {
            var r = snap.Ref(id)!;
            Assert.Equal((id, name, name, group, (string?)null), (r.Id, r.Name, r.Label, r.PositionGroup, r.ShortName));
        }
        Assert.Equal(expected.Length, ArchetypeDefaults.Known.Count);

        // fora de 1..13 (ex.: o id 30 observado): sem nome, rótulo padrão
        var unnamed = snap.Ref(30)!;
        Assert.Equal((30, (string?)null, "Arquétipo #30"), (unnamed.Id, unnamed.Name, unnamed.Label));
        Assert.Equal("ATAQUE", unnamed.PositionGroup); // inferido: só foi usado por atacante
        Assert.Equal("Arquétipo #14", snap.Ref(14)!.Label);
        Assert.Null(snap.Ref(14)!.PositionGroup);      // nunca observado: sem grupo
    }

    [Fact]
    public async Task IdZeroOrNegativeResolvesToNull()
    {
        using var seed = Seeded();
        var snap = await seed.Catalog().GetAsync();
        Assert.Null(snap.Ref(0));
        Assert.Null(snap.Ref(-1));
    }

    [Fact]
    public async Task CatalogRowOverridesTheDefaultAndAClearedNameFallsBackToTheLabel()
    {
        using var seed = Seeded();
        seed.Db.PlayerArchetypes.AddRange(
            new PlayerArchetypeEntity { Id = 8, Name = " Maestro BR ", ShortName = "MAE", PositionGroup = null, UpdatedAtUtc = DateTime.UtcNow },
            new PlayerArchetypeEntity { Id = 5, Name = null, PositionGroup = "MEIO", UpdatedAtUtc = DateTime.UtcNow },
            new PlayerArchetypeEntity { Id = 30, Name = "Isolado", PositionGroup = "defesa", UpdatedAtUtc = DateTime.UtcNow });
        seed.Db.SaveChanges();

        var snap = await seed.Catalog().GetAsync();
        var maestro = snap.Ref(8)!;
        Assert.Equal(("Maestro BR", "Maestro BR", "MAE", "MEIO"), (maestro.Name, maestro.Label, maestro.ShortName, maestro.PositionGroup)); // grupo nulo -> inferido
        var five = snap.Ref(5)!; // linha existente vence o padrão, mesmo sem nome
        Assert.Equal(((string?)null, "Arquétipo #5", "MEIO"), (five.Name, five.Label, five.PositionGroup));
        Assert.Equal("DEFESA", snap.Ref(30)!.PositionGroup); // valor do catálogo (normalizado) vence a inferência
        Assert.Contains(snap.All(), r => r.Id == 30);
    }

    [Fact]
    public void GroupInferencePicksTheMostPlayedGroupAndIgnoresEmptyPositions()
    {
        Assert.Equal("ATAQUE", ArchetypeGroups.Infer(new[] { ("forward", 5), ("midfielder", 2), ("", 50) }));
        Assert.Equal("DEFESA", ArchetypeGroups.Infer(new[] { ("defender", 3), ("CB", 1), ("goalkeeper", 3) }));
        Assert.Equal("ATAQUE", ArchetypeGroups.Infer(new[] { ("forward", 2), ("midfielder", 2) })); // empate: ordem ATAQUE, MEIO...
        Assert.Null(ArchetypeGroups.Infer(new[] { ("", 4) }));
        Assert.Null(ArchetypeGroups.Infer(Array.Empty<(string, int)>()));
    }

    [Fact]
    public async Task CatalogIsCachedAndReloadedAfterInvalidate()
    {
        using var seed = Seeded();
        var catalog = new CountingCatalog(seed.ScopeFactory(), NullLogger<ArchetypeCatalog>.Instance);

        var first = await catalog.GetAsync();
        Assert.Same(first, await catalog.GetAsync());
        Assert.Equal(1, catalog.Reads);

        catalog.Invalidate();
        var second = await catalog.GetAsync();
        Assert.Equal(2, catalog.Reads);
        Assert.NotEqual(first.Version, second.Version);
    }

    // ------------------------------------------------------------------ resiliência: tabela inexistente

    [Fact]
    public async Task MissingCatalogTableFallsBackToTheDefaultsAndWarnsOnlyOnce()
    {
        using var seed = Seeded();
        var logger = new CountingLogger();
        var catalog = new MissingTableCatalog(seed.ScopeFactory(), logger);

        var snap = await catalog.GetAsync();
        Assert.False(snap.Available);
        Assert.Equal("Maestro", snap.Ref(8)!.Label);          // mapa conhecido continua valendo sem a tabela
        Assert.Equal("Arquétipo #30", snap.Ref(30)!.Label);   // o resto vira rótulo padrão
        Assert.Equal("ATAQUE", snap.Ref(30)!.PositionGroup);  // a inferência (MatchPlayers) não depende da tabela
        Assert.Equal(1, logger.Warnings);

        catalog.Invalidate();
        await catalog.GetAsync();
        catalog.Invalidate();
        await catalog.GetAsync();
        Assert.Equal(3, catalog.Reads);
        Assert.Equal(1, logger.Warnings); // um único aviso
    }

    [Fact]
    public async Task EndpointsKeepWorkingWhenTheCatalogTableIsMissing()
    {
        using var seed = OneNightSeeded(out var nightId);
        var catalog = new MissingTableCatalog(seed.ScopeFactory(), NullLogger<ArchetypeCatalog>.Instance);

        var cards = await seed.Cards(catalog).GetCardsAsync(AnalyticsSeed.Club, null, null, null, 1, default);
        Assert.Equal(2, cards.Cards.Count);
        Assert.Equal("Maestro", cards.Cards.Single(c => c.PlayerEntityId == 11).Archetype!.Label);

        var night = await seed.Nights(catalog).GetNightAsync(AnalyticsSeed.Club, nightId, null, default);
        Assert.NotNull(night);
        Assert.Equal("Maestro", night!.Players.Single(p => p.PlayerEntityId == 11).Archetype!.Label);

        var wrapped = await seed.Wrapped(catalog).GetWrappedAsync(AnalyticsSeed.Club, null, default);
        Assert.Equal(8, wrapped.Archetypes.MostUsed!.Archetype.Id);

        var summary = await seed.Archetypes(catalog).GetSummaryAsync(AnalyticsSeed.Club, null, null, null, default);
        Assert.Equal(2, summary.Archetypes.Count);

        var profile = await new PlayerService(new PlayerRepository(seed.Db), new MatchRepository(seed.Db), NullLogger<PlayerService>.Instance, catalog)
            .GetProfileAsync(11, default);
        Assert.Equal(2, profile.Archetypes.Count);

        // o Admin lista mesmo sem a tabela e o PUT responde com a exceção tipada (o controller devolve 503)
        Assert.NotEmpty(await seed.Archetypes(catalog).GetAdminListAsync(default));
    }

    private static AnalyticsSeed OneNightSeeded(out long nightId)
    {
        var (seed, id) = OneNight();
        nightId = id;
        return seed;
    }

    // ------------------------------------------------------------------ uso, principal e trocas

    private static ArchetypeSample S(int arch, int day, long match, double rating = 7, int? ovr = null, string? ovrStr = null, int goals = 0, int assists = 0) =>
        new(arch, Day(day), match, rating, ovr, ovrStr, goals, assists);

    [Fact]
    public void UsagesCountZerosInThePercentageButNotInTheList()
    {
        var samples = new[] { S(8, 0, 1, 7, ovr: 80, goals: 1), S(8, 1, 2, 8, ovrStr: "82", assists: 1), S(0, 2, 3), S(9, 3, 4, 0) };
        var usages = ArchetypeUsageCalc.Usages(samples, ArchetypeRef.Unresolved);

        Assert.Equal(new[] { 8, 9 }, usages.Select(u => u.Archetype.Id));
        var u8 = usages[0];
        Assert.Equal((2, 50.0, 7.5, 81.0, 1, 1), (u8.Matches, u8.Pct, u8.AvgRating, u8.AvgProOverall, u8.Goals, u8.Assists));
        Assert.Equal((Day(0), Day(1)), (u8.FirstPlayedAt, u8.LastPlayedAt));
        var u9 = usages[1];
        Assert.Equal((1, 25.0), (u9.Matches, u9.Pct));
        Assert.Null(u9.AvgRating);     // nota 0 não conta
        Assert.Null(u9.AvgProOverall); // sem overall
        Assert.Equal("Arquétipo #8", u8.Archetype.Label);
    }

    [Fact]
    public void PrincipalTieGoesToTheMostRecentAndEmptyInputHasNoData()
    {
        var tie = ArchetypeUsageCalc.Usages(new[] { S(8, 0, 1), S(9, 1, 2), S(8, 2, 3), S(9, 3, 4) }, ArchetypeRef.Unresolved);
        Assert.Equal(9, ArchetypeUsageCalc.Principal(tie)!.Id);

        var none = ArchetypeUsageCalc.Usages(new[] { S(0, 0, 1) }, ArchetypeRef.Unresolved);
        Assert.Empty(none);
        Assert.Null(ArchetypeUsageCalc.Principal(none));
    }

    [Fact]
    public void ChangesAreRealSwitchesInChronologicalOrderAndIgnoreZeros()
    {
        var samples = new[] { S(9, 5, 5), S(8, 0, 1), S(8, 1, 2), S(0, 2, 3), S(8, 3, 4), S(9, 4, 6) };
        var changes = ArchetypeUsageCalc.Changes(samples, ArchetypeRef.Unresolved);

        var c = Assert.Single(changes);
        Assert.Equal((8, 9, 6L, Day(4)), (c.From!.Id, c.To.Id, c.MatchId, c.At));
        Assert.Empty(ArchetypeUsageCalc.Changes(new[] { S(8, 0, 1), S(0, 1, 2), S(8, 2, 3) }, ArchetypeRef.Unresolved)); // 0 no meio não é troca
        Assert.Empty(ArchetypeUsageCalc.Changes(new[] { S(0, 0, 1), S(8, 1, 2) }, ArchetypeRef.Unresolved));              // primeiro uso não é troca
    }

    // ------------------------------------------------------------------ resumo

    [Fact]
    public async Task SummaryAveragesPerArchetypeIgnoringZerosAndNulls()
    {
        using var seed = Seeded();
        var s = await seed.Archetypes().GetSummaryAsync(AnalyticsSeed.Club, null, null, null, default);

        Assert.Equal((9, 1), (s.TotalPlayerMatches, s.WithoutArchetype));
        Assert.Equal(new[] { 12, 8, 9, 30 }, s.Archetypes.Select(a => a.Archetype.Id)); // jogos desc, empate por id

        var finisher = s.Archetypes[0];
        Assert.Equal(("Finisher", 3, 1), (finisher.Archetype.Label, finisher.Matches, finisher.Players));
        Assert.Equal((7.67, 1.0, 66.67), (finisher.AvgRating, finisher.GoalsPerMatch, finisher.WinPct));
        Assert.Null(finisher.AvgProOverall);
        Assert.Null(finisher.PassAccuracyPct); // sem tentativas de passe: nulo, não 0
        Assert.Null(finisher.ShotAccuracyPct);

        var maestro = s.Archetypes[1];
        Assert.Equal((2, 7.5, 81.0, 80, 82), (maestro.Matches, maestro.AvgRating, maestro.AvgProOverall, maestro.MinProOverall, maestro.MaxProOverall));
        Assert.Equal((0.5, 0.5, 50.0), (maestro.GoalsPerMatch, maestro.AssistsPerMatch, maestro.WinPct));
        var top = Assert.Single(maestro.TopPlayers);
        Assert.Equal((11L, 2, 81.0), (top.PlayerEntityId, top.Matches, top.AvgProOverall));

        var creator = s.Archetypes[2]; // ProOverall só em 1 das 2 partidas
        Assert.Equal((76.0, 76, 76), (creator.AvgProOverall, creator.MinProOverall, creator.MaxProOverall));

        // por jogador: o denominador do % inclui a partida sem arquétipo
        var p11 = s.ByPlayer.Single(p => p.PlayerEntityId == 11);
        Assert.Equal(1, p11.Switches);
        Assert.Equal(new[] { 9, 8 }, p11.Archetypes.Select(a => a.Archetype.Id)); // empate 2x2: o mais recente (9) primeiro
        Assert.Equal(40.0, p11.Archetypes[0].Pct);
        Assert.Equal(2, s.ByPlayer.Single(p => p.PlayerEntityId == 12).Switches); // 12 -> 30 -> 12
        Assert.Equal(new long[] { 11, 12 }, s.ByPlayer.Select(p => p.PlayerEntityId)); // mais jogos primeiro
    }

    [Fact]
    public async Task SummaryRespectsTheDateRangeAndIsEmptyWithoutData()
    {
        using var seed = Seeded();
        var range = await seed.Archetypes().GetSummaryAsync(AnalyticsSeed.Club, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 2), null, default);
        Assert.Equal(4, range.TotalPlayerMatches);
        Assert.Equal(new[] { 8, 12 }, range.Archetypes.Select(a => a.Archetype.Id));

        var empty = await seed.Archetypes().GetSummaryAsync(999, null, null, null, default);
        Assert.Equal((0, 0), (empty.TotalPlayerMatches, empty.Archetypes.Count));
    }

    // ------------------------------------------------------------------ cartas

    [Fact]
    public async Task CardsListUsageAndPrincipalWithoutChangingTheScoring()
    {
        using var seed = Seeded();
        var with = await seed.Cards().GetCardsAsync(AnalyticsSeed.Club, null, null, null, 1, default);
        var without = await seed.Cards(new NoCatalog()).GetCardsAsync(AnalyticsSeed.Club, null, null, null, 1, default);

        var card = with.Cards.Single(c => c.PlayerEntityId == 11);
        Assert.Equal(9, card.Archetype!.Id); // 2x2: o mais recente
        Assert.Equal(new[] { 9, 8 }, card.Archetypes.Select(a => a.Archetype.Id));
        Assert.Null(with.ArchetypeId);

        // a pontuação é idêntica com e sem catálogo/arquétipos
        foreach (var c in with.Cards)
        {
            var other = without.Cards.Single(x => x.PlayerEntityId == c.PlayerEntityId);
            Assert.Equal((other.Overall, other.Tier, other.Axes.Ata, other.Axes.Pas, other.Axes.Cri, other.Axes.Def, other.Axes.Imp, other.Axes.Reg),
                (c.Overall, c.Tier, c.Axes.Ata, c.Axes.Pas, c.Axes.Cri, c.Axes.Def, c.Axes.Imp, c.Axes.Reg));
        }
    }

    private sealed class NoCatalog : EAFCMatchTracker.Application.Interfaces.Services.IArchetypeCatalog
    {
        public Task<ArchetypeCatalogSnapshot> GetAsync(CancellationToken ct = default) => Task.FromResult(ArchetypeCatalogSnapshot.Empty);
        public void Invalidate() { }
    }

    [Fact]
    public async Task ArchetypeFilterRecomputesTheCardsAndAvailableArchetypesIgnoreTheFilter()
    {
        using var seed = Seeded();
        var filtered = await seed.Cards().GetCardsAsync(AnalyticsSeed.Club, null, null, null, 1, 8, default);

        Assert.Equal(8, filtered.ArchetypeId);
        Assert.Equal(5, filtered.TotalMatches); // jogos do clube no período não mudam
        var card = Assert.Single(filtered.Cards);  // só quem jogou como arquétipo 8
        Assert.Equal((11L, 2, 1, 1), (card.PlayerEntityId, card.Matches, card.Stats.Goals, card.Stats.Assists));
        Assert.Equal(8, card.Archetype!.Id);
        Assert.Equal(new[] { 9, 8 }, card.Archetypes.Select(a => a.Archetype.Id)); // uso do período, sem o filtro

        // opções do filtro: calculadas sem o filtro (todas as quatro, com jogadores/partidas)
        Assert.Equal(new[] { 12, 8, 9, 30 }, filtered.AvailableArchetypes.Select(a => a.Archetype.Id));
        var maestro = filtered.AvailableArchetypes.Single(a => a.Archetype.Id == 8);
        Assert.Equal(("Maestro", 1, 2), (maestro.Archetype.Label, maestro.Players, maestro.Matches));

        var unknown = await seed.Cards().GetCardsAsync(AnalyticsSeed.Club, null, null, null, 1, 77, default);
        Assert.Empty(unknown.Cards);
        Assert.Equal(4, unknown.AvailableArchetypes.Count);
    }

    [Fact]
    public async Task FilteredPreAssistsOnlyCountMatchesPlayedWithThatArchetype()
    {
        using var seed = new AnalyticsSeed();
        var m1 = seed.Match(Day(0), 2, 0, ours: new[] { new Pl(11, Pos: "midfielder", Arch: 8), new Pl(12, Goals: 1, Arch: 12) });
        var m2 = seed.Match(Day(1), 1, 0, ours: new[] { new Pl(11, Pos: "midfielder", Arch: 9), new Pl(12, Goals: 1, Arch: 12) });
        seed.Goal(m1, 12, 12, 11);
        seed.Goal(m2, 12, 12, 11);

        var all = await seed.Cards().GetCardsAsync(AnalyticsSeed.Club, null, null, null, 1, default);
        Assert.Equal(2, all.Cards.Single(c => c.PlayerEntityId == 11).Stats.PreAssists);
        var as8 = await seed.Cards().GetCardsAsync(AnalyticsSeed.Club, null, null, null, 1, 8, default);
        Assert.Equal(1, as8.Cards.Single(c => c.PlayerEntityId == 11).Stats.PreAssists);
    }

    [Fact]
    public async Task CompareCarriesArchetypesOnBothSidesAndAcceptsTheFilter()
    {
        using var seed = Seeded();
        var cmp = await seed.Cards().GetCompareAsync(AnalyticsSeed.Club, 11, 12, null, null, null, default);
        Assert.Equal(9, cmp!.A.Archetype!.Id);
        Assert.Equal(12, cmp.B.Archetype!.Id);
        Assert.Equal(new[] { 12, 30 }, cmp.B.Archetypes.Select(a => a.Archetype.Id));

        var filtered = await seed.Cards().GetCompareAsync(AnalyticsSeed.Club, 11, 12, null, null, null, 8, default);
        Assert.Equal(8, filtered!.ArchetypeId);
        Assert.Equal((2, 0), (filtered.A.Matches, filtered.B.Matches)); // 12 nunca jogou como 8: carta zerada
        Assert.Equal(new[] { 12, 30 }, filtered.B.Archetypes.Select(a => a.Archetype.Id)); // mas o uso dele segue visível
        Assert.Null(filtered.B.Archetype);
    }

    [Fact]
    public async Task ControllerValidatesTheArchetypeIdQuery()
    {
        using var seed = Seeded();
        var c = new PlayerCardsController(seed.Cards());

        var ok = Assert.IsType<OkObjectResult>(await c.Cards(AnalyticsSeed.Club, null, null, null, "1", default, archetypeId: "8"));
        Assert.Equal(8, Assert.IsType<PlayerCardsDto>(ok.Value).ArchetypeId);
        Assert.IsType<OkObjectResult>(await c.Cards(AnalyticsSeed.Club, null, null, null, "1", default, archetypeId: ""));
        foreach (var bad in new[] { "0", "-1", "256", "x", "1.5" })
        {
            var r = Assert.IsType<ObjectResult>(await c.Cards(AnalyticsSeed.Club, null, null, null, "1", default, archetypeId: bad));
            Assert.Equal(400, r.StatusCode);
            Assert.Equal("archetypeId deve ser um número inteiro entre 1 e 255.", Assert.IsType<ProblemDetails>(r.Value).Detail);
        }
        Assert.IsType<ObjectResult>(await c.Compare(AnalyticsSeed.Club, "11", "12", null, null, null, default, archetypeId: "999"));
        Assert.IsType<OkObjectResult>(await c.Compare(AnalyticsSeed.Club, "11", "12", null, null, null, default, archetypeId: "9"));
    }

    // ------------------------------------------------------------------ perfil

    [Fact]
    public async Task ProfileHasPerMatchArchetypeUsageAndChanges()
    {
        using var seed = Seeded();
        var svc = new PlayerService(new PlayerRepository(seed.Db), new MatchRepository(seed.Db), NullLogger<PlayerService>.Instance, seed.Catalog());

        var p = await svc.GetProfileAsync(11, default);
        Assert.Equal(new[] { 9, 8 }, p.Archetypes.Select(a => a.Archetype.Id));
        var change = Assert.Single(p.ArchetypeChanges);
        Assert.Equal(("Maestro", "Creator"), (change.From!.Label, change.To.Label));
        Assert.Equal(3, change.MatchId);

        // histórico: do mais novo para o mais antigo; a partida sem dado vem com archetype nulo
        Assert.Equal(new short[] { 9, 0, 9, 8, 8 }, p.History.Select(h => h.ArchetypeId));
        Assert.Null(p.History[1].Archetype);
        Assert.Equal("Creator", p.History[0].Archetype!.Label);
    }

    // ------------------------------------------------------------------ noite de jogo e retrospectiva

    [Fact]
    public async Task GameNightPlayersCarryTheMainArchetypeAndUsage()
    {
        var (seed, nightId) = OneNight();
        using var _ = seed;
        var night = await seed.Nights(seed.Catalog()).GetNightAsync(AnalyticsSeed.Club, nightId, null, default);

        var p11 = night!.Players.Single(p => p.PlayerEntityId == 11);
        Assert.Equal(8, p11.Archetype!.Id);
        Assert.Equal(new[] { 8, 9 }, p11.Archetypes.Select(a => a.Archetype.Id));
        Assert.Equal((2, 66.67), (p11.Archetypes[0].Matches, p11.Archetypes[0].Pct));

        var p12 = night.Players.Single(p => p.PlayerEntityId == 12);
        Assert.Null(p12.Archetype);
        Assert.Empty(p12.Archetypes);
    }

    [Fact]
    public async Task WrappedHasClubLevelAndPerPlayerArchetypes()
    {
        var (seed, _) = OneNight();
        using var _s = seed;
        var w = await seed.Wrapped(seed.Catalog()).GetWrappedAsync(AnalyticsSeed.Club, null, default);

        Assert.Equal(8, w.Archetypes.MostUsed!.Archetype.Id);
        Assert.Equal((2, 33.33), (w.Archetypes.MostUsed.Matches, w.Archetypes.MostUsed.Pct)); // 2 de 6 linhas (as do 12 não têm dado)
        Assert.Equal(1, w.Archetypes.Switches); // 11: 8 -> 9
        Assert.Equal(new[] { 8, 9 }, w.Archetypes.List.Select(a => a.Archetype.Id));
        Assert.Equal(8, w.Players.TopScorer!.Archetype!.Id);
    }

    // ------------------------------------------------------------------ admin

    [Fact]
    public void AdminValidationNormalizesAndRejectsBadInput()
    {
        Assert.Null(ArchetypeService.Validate(8, new AdminArchetypeUpdateDto { Name = "  Maestro ", ShortName = "", PositionGroup = "meio" }, out var n));
        Assert.Equal(("Maestro", null, "MEIO"), (n.Name, n.ShortName, n.PositionGroup));
        Assert.Null(ArchetypeService.Validate(1, new AdminArchetypeUpdateDto(), out var empty));
        Assert.Equal(((string?)null, (string?)null, (string?)null), (empty.Name, empty.ShortName, empty.PositionGroup));
        Assert.Null(ArchetypeService.Validate(255, new AdminArchetypeUpdateDto { Name = new string('a', 40), ShortName = new string('b', 8) }, out _));

        Assert.NotNull(ArchetypeService.Validate(0, new AdminArchetypeUpdateDto(), out _));
        Assert.NotNull(ArchetypeService.Validate(256, new AdminArchetypeUpdateDto(), out _));
        Assert.NotNull(ArchetypeService.Validate(8, null, out _));
        Assert.Contains("40", ArchetypeService.Validate(8, new AdminArchetypeUpdateDto { Name = new string('a', 41) }, out _));
        Assert.Contains("8", ArchetypeService.Validate(8, new AdminArchetypeUpdateDto { ShortName = "123456789" }, out _));
        Assert.Contains("ATAQUE", ArchetypeService.Validate(8, new AdminArchetypeUpdateDto { PositionGroup = "LATERAL" }, out _));
    }

    [Fact]
    public async Task AdminListsObservedAndCatalogIdsAndUpsertRefreshesTheCatalog()
    {
        using var seed = Seeded();
        var catalog = seed.Catalog();
        var svc = seed.Archetypes(catalog);

        var list = await svc.GetAdminListAsync(default);
        Assert.Equal(Enumerable.Range(1, 13).Concat(new[] { 30 }), list.Select(a => a.Id)); // os 13 conhecidos + o 30 observado
        var maestro = list.Single(a => a.Id == 8);
        Assert.Equal(("Maestro", "MEIO", (string?)"MEIO", 2, 1, (double?)81.0), (maestro.Name, maestro.PositionGroup, maestro.InferredPositionGroup, maestro.Matches, maestro.Players, maestro.AvgProOverall));
        Assert.Null(maestro.UpdatedAt); // ainda é o padrão em código
        Assert.Equal((11L, 2), (maestro.TopPlayers[0].PlayerEntityId, maestro.TopPlayers[0].Matches));
        var thirty = list.Single(a => a.Id == 30);
        Assert.Equal(((string?)null, (string?)null, "ATAQUE", 1), (thirty.Name, thirty.PositionGroup, thirty.InferredPositionGroup, thirty.Matches));
        Assert.Equal((0, 0), (list.Single(a => a.Id == 13).Matches, list.Single(a => a.Id == 13).Players));

        // upsert cria a linha, atualiza o cache do catálogo e é idempotente
        Assert.Equal("Arquétipo #30", (await catalog.GetAsync()).Ref(30)!.Label);
        var saved = await svc.UpdateAsync(30, new AdminArchetypeUpdateDto { Name = "Nome novo", ShortName = "NN", PositionGroup = "ATAQUE" }, default);
        Assert.Equal(("Nome novo", "NN", "ATAQUE"), (saved.Name, saved.ShortName, saved.PositionGroup));
        Assert.NotNull(saved.UpdatedAt);
        Assert.Equal("Nome novo", (await catalog.GetAsync()).Ref(30)!.Label);

        await svc.UpdateAsync(30, new AdminArchetypeUpdateDto { Name = "Outro" }, default);
        Assert.Equal(1, seed.Db.PlayerArchetypes.Count(a => a.Id == 30));
        Assert.Equal("Outro", seed.Db.PlayerArchetypes.Single(a => a.Id == 30).Name);
        Assert.Null(seed.Db.PlayerArchetypes.Single(a => a.Id == 30).ShortName);

        // editar um id conhecido grava a linha e passa a valer sobre o padrão
        await svc.UpdateAsync(5, new AdminArchetypeUpdateDto { Name = "Disruptor+", ShortName = "DIS" }, default);
        Assert.Equal("Disruptor+", (await catalog.GetAsync()).Ref(5)!.Label);
    }

    [Fact]
    public async Task MatchStatisticsAndProfilePlayersCarryTheArchetype()
    {
        using var seed = Seeded();
        var svc = new MatchService(new MatchRepository(seed.Db), new PlayerRepository(seed.Db), seed.Db,
            new MemoryCache(new MemoryCacheOptions()), NullLogger<MatchService>.Instance, seed.Catalog());

        var stats = await svc.GetMatchStatisticsByIdAsync(3, default);
        var p11 = stats!.Players.Single(p => p.PlayerEntityId == 11);
        Assert.Equal((9, "Creator"), ((int)p11.ArchetypeId, p11.Archetype!.Label));
        Assert.Equal(30, stats.Players.Single(p => p.PlayerEntityId == 12).ArchetypeId);

        var noData = (await svc.GetMatchStatisticsByIdAsync(4, default))!.Players.Single(p => p.PlayerEntityId == 11);
        Assert.Equal(0, noData.ArchetypeId);
        Assert.Null(noData.Archetype);
        Assert.Empty(noData.Archetypes);

        var match = await svc.GetMatchByIdAsync(1, default);
        Assert.Equal(new[] { "Maestro", "Finisher" }, match!.Players.OrderBy(p => p.Id).Select(p => p.Archetype!.Label));
    }
    // ------------------------------------------------------------------ onda 2: filtro de posição

    [Fact]
    public async Task PositionFilterRestrictsCardsAndOffersOnlyThatPositionsArchetypes()
    {
        using var seed = Seeded();
        var r = await seed.Cards().GetCardsAsync(AnalyticsSeed.Club, null, null, null, 1, null, "ATAQUE", default);

        Assert.Equal(("ATAQUE", (int?)null), (r.PositionGroup, r.ArchetypeId));
        var card = Assert.Single(r.Cards); // só o atacante (12)
        Assert.Equal((12L, 4), (card.PlayerEntityId, card.Matches));
        Assert.Equal(new[] { 12, 30 }, card.Archetypes.Select(a => a.Archetype.Id));

        // 2º filtro segue a posição: só arquétipos jogados por atacantes (sem filtro de arquétipo aplicado)
        Assert.Equal(new[] { 12, 30 }, r.AvailableArchetypes.Select(a => a.Archetype.Id));
        // posições: calculadas sem nenhum filtro
        Assert.Equal(new[] { ("MEIO", 5), ("ATAQUE", 4) }, r.AvailablePositionGroups.Select(g => (g.PositionGroup, g.Matches)));

        var all = await seed.Cards().GetCardsAsync(AnalyticsSeed.Club, null, null, null, 1, null, null, default);
        Assert.Equal(new[] { 12, 8, 9, 30 }, all.AvailableArchetypes.Select(a => a.Archetype.Id));
        Assert.Equal(2, all.Cards.Count);
        Assert.Null(all.PositionGroup);

        var none = await seed.Cards().GetCardsAsync(AnalyticsSeed.Club, null, null, null, 1, null, "GOLEIRO", default);
        Assert.Empty(none.Cards);
        Assert.Empty(none.AvailableArchetypes);
        Assert.Equal(2, none.AvailablePositionGroups.Count);
    }

    [Fact]
    public async Task PositionAndArchetypeCombineWithAnd()
    {
        using var seed = Seeded();
        var r = await seed.Cards().GetCardsAsync(AnalyticsSeed.Club, null, null, null, 1, 9, "MEIO", default);
        var card = Assert.Single(r.Cards);
        Assert.Equal((11L, 2, 1), (card.PlayerEntityId, card.Matches, card.Stats.Goals));
        Assert.Equal(9, card.Archetype!.Id);
        Assert.Equal(("MEIO", (int?)9), (r.PositionGroup, r.ArchetypeId));
        Assert.Equal(new[] { 8, 9 }, r.AvailableArchetypes.Select(a => a.Archetype.Id).OrderBy(i => i)); // sem o filtro de arquétipo

        // arquétipo de atacante + posição meio: nenhuma linha
        Assert.Empty((await seed.Cards().GetCardsAsync(AnalyticsSeed.Club, null, null, null, 1, 12, "MEIO", default)).Cards);
    }

    [Fact]
    public async Task PositionFilterAppliesToCompareAndSummary()
    {
        using var seed = Seeded();
        var cmp = await seed.Cards().GetCompareAsync(AnalyticsSeed.Club, 11, 12, null, null, null, null, "MEIO", default);
        Assert.Equal(("MEIO", 5, 0), (cmp!.PositionGroup, cmp.A.Matches, cmp.B.Matches)); // 12 nunca jogou de meia: carta zerada

        var s = await seed.Archetypes().GetSummaryAsync(AnalyticsSeed.Club, null, null, null, null, "ATAQUE", default);
        Assert.Equal(("ATAQUE", 4, 0), (s.PositionGroup, s.TotalPlayerMatches, s.WithoutArchetype));
        Assert.Equal(new[] { 12, 30 }, s.Archetypes.Select(a => a.Archetype.Id));
        Assert.Equal(new[] { 12, 30 }, s.AvailableArchetypes.Select(a => a.Archetype.Id));
        Assert.Equal(2, s.AvailablePositionGroups.Count);

        var both = await seed.Archetypes().GetSummaryAsync(AnalyticsSeed.Club, null, null, null, 12, "ATAQUE", default);
        Assert.Equal(12, both.ArchetypeId);
        Assert.Equal(3, both.TotalPlayerMatches);
        Assert.Equal(new[] { 12 }, both.Archetypes.Select(a => a.Archetype.Id));
        Assert.Equal(2, both.AvailableArchetypes.Count); // opções NÃO somem ao escolher o arquétipo
    }

    [Fact]
    public async Task ProfileFiltersHistoryAndUsageButKeepsTheTotals()
    {
        using var seed = Seeded();
        var svc = new PlayerService(new PlayerRepository(seed.Db), new MatchRepository(seed.Db), NullLogger<PlayerService>.Instance, seed.Catalog());

        var full = await svc.GetProfileAsync(11, null, null, default);
        Assert.Equal(5, full.FilteredSummary.Matches); // sem filtro = tudo
        Assert.Equal((3, 1, 1), (full.FilteredSummary.Wins, full.FilteredSummary.Draws, full.FilteredSummary.Losses));

        var p = await svc.GetProfileAsync(11, 8, "MEIO", default);
        Assert.Equal((5, 3), (p.TotalMatches, p.TotalWins)); // totais gerais intactos
        Assert.Equal(new long[] { 2, 1 }, p.History.Select(h => h.MatchId));
        Assert.Equal(((int?)8, "MEIO"), (p.ArchetypeId, p.PositionGroup));
        var fs = p.FilteredSummary;
        Assert.Equal((2, 1, 1, 0), (fs.Matches, fs.Wins, fs.Draws, fs.Losses));
        Assert.Equal((1, 1, (double?)7.5, (double?)81.0), (fs.Goals, fs.Assists, fs.AvgRating, fs.AvgProOverall));
        // uso: posição aplicada, arquétipo NÃO (a seção segue mostrando os outros)
        Assert.Equal(new[] { 9, 8 }, p.Archetypes.Select(a => a.Archetype.Id));
        Assert.Equal(new[] { 8, 9 }, p.AvailableArchetypes.Select(a => a.Archetype.Id).OrderBy(i => i));
        Assert.Equal(new[] { ("MEIO", 5) }, p.AvailablePositionGroups.Select(g => (g.PositionGroup, g.Matches)));

        var wrongPos = await svc.GetProfileAsync(11, null, "ATAQUE", default);
        Assert.Empty(wrongPos.History);
        Assert.Equal(0, wrongPos.FilteredSummary.Matches);
        Assert.Null(wrongPos.FilteredSummary.AvgRating);
        Assert.Equal(5, wrongPos.TotalMatches);
    }

    [Fact]
    public async Task ControllersRejectAnInvalidPositionGroup()
    {
        using var seed = Seeded();
        var c = new PlayerCardsController(seed.Cards());
        Assert.IsType<OkObjectResult>(await c.Cards(AnalyticsSeed.Club, null, null, null, "1", default, positionGroup: "ataque"));
        Assert.IsType<OkObjectResult>(await c.Cards(AnalyticsSeed.Club, null, null, null, "1", default, positionGroup: ""));
        var bad = Assert.IsType<ObjectResult>(await c.Cards(AnalyticsSeed.Club, null, null, null, "1", default, positionGroup: "LATERAL"));
        Assert.Equal(400, bad.StatusCode);
        Assert.Contains("ATAQUE", Assert.IsType<ProblemDetails>(bad.Value).Detail);
        Assert.Equal(400, ((ObjectResult)await c.Compare(AnalyticsSeed.Club, "11", "12", null, null, null, default, positionGroup: "x")).StatusCode);
        Assert.IsType<OkObjectResult>(await c.Compare(AnalyticsSeed.Club, "11", "12", null, null, null, default, positionGroup: "MEIO"));
    }

    // ------------------------------------------------------------------ onda 2: segmentos da noite

    [Fact]
    public async Task NightSegmentsOnlyExistWithMoreThanOneArchetypePositionCombination()
    {
        using var seed = new AnalyticsSeed();
        var m1 = seed.Match(T0, 1, 0, ours: new[]
        {
            new Pl(11, Goals: 1, Pos: "midfielder", Arch: 8, Rating: 7), new Pl(12, Pos: "forward", Arch: 12, Rating: 6),
            new Pl(13, Pos: "defender", Arch: 4, Rating: 6), new Pl(14, Pos: "forward", Rating: 6)
        });
        seed.Match(T0.AddMinutes(40), 2, 0, ours: new[]
        {
            new Pl(11, Goals: 1, Assists: 1, Pos: "midfielder", Arch: 9, Rating: 8), new Pl(12, Pos: "forward", Arch: 12, Rating: 6),
            new Pl(13, Pos: "midfielder", Arch: 4, Rating: 7), new Pl(14, Pos: "forward", Rating: 6)
        });
        seed.Match(T0.AddMinutes(80), 0, 1, ours: new[]
        {
            new Pl(11, Pos: "midfielder", Arch: 9, Rating: 6), new Pl(12, Pos: "forward", Arch: 12, Rating: 6),
            new Pl(13, Pos: "defender", Arch: 4, Rating: 6), new Pl(14, Pos: "forward", Rating: 6)
        });
        var night = await seed.Nights(seed.Catalog()).GetNightAsync(AnalyticsSeed.Club, m1, null, default);

        // 12 (1 combinação) e 14 (sem arquétipo, 1 posição): sem segmentos
        Assert.Empty(night!.Players.Single(p => p.PlayerEntityId == 12).Segments);
        Assert.Empty(night.Players.Single(p => p.PlayerEntityId == 14).Segments);

        // 11 trocou de arquétipo: 2 segmentos que somam o total da linha principal
        var p11 = night.Players.Single(p => p.PlayerEntityId == 11);
        Assert.Equal(new[] { 9, 8 }, p11.Segments.Select(s => s.Archetype!.Id));
        Assert.Equal((2, 1, 1), (p11.Segments[0].Matches, p11.Segments[0].Goals, p11.Segments[0].Assists));
        Assert.Equal(("midfielder", "MEIO", 7.0), (p11.Segments[0].Position, p11.Segments[0].PositionGroup, p11.Segments[0].AvgRating));
        Assert.Equal((p11.Matches, p11.Goals, p11.Assists, p11.Motm, p11.RedCards),
            (p11.Segments.Sum(s => s.Matches), p11.Segments.Sum(s => s.Goals), p11.Segments.Sum(s => s.Assists), p11.Segments.Sum(s => s.Motm), p11.Segments.Sum(s => s.RedCards)));

        // 13 mesmo arquétipo mas posição de outro grupo: também separa
        var p13 = night.Players.Single(p => p.PlayerEntityId == 13);
        Assert.Equal(new[] { "DEFESA", "MEIO" }, p13.Segments.Select(s => s.PositionGroup));
        Assert.All(p13.Segments, s => Assert.Equal(4, s.Archetype!.Id));
        Assert.Equal(p13.Matches, p13.Segments.Sum(s => s.Matches));
    }

    [Fact]
    public async Task NightSegmentsKeepTheNoDataArchetypeAsNull()
    {
        using var seed = new AnalyticsSeed();
        var m1 = seed.Match(T0, 1, 0, ours: new[] { new Pl(11, Pos: "midfielder", Arch: 8) });
        seed.Match(T0.AddMinutes(40), 1, 0, ours: new[] { new Pl(11, Pos: "midfielder", Arch: 0) });
        var night = await seed.Nights(seed.Catalog()).GetNightAsync(AnalyticsSeed.Club, m1, null, default);

        var p = night!.Players.Single();
        Assert.Equal(2, p.Segments.Count);
        Assert.Contains(p.Segments, s => s.Archetype is null);
        Assert.Contains(p.Segments, s => s.Archetype?.Id == 8);
        Assert.Equal(8, p.Archetype!.Id);
    }
}
