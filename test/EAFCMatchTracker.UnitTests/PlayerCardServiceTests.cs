using EAFCMatchTracker.Application.Dtos;
using EAFCMatchTracker.Application.Services.Analytics;
using EAFCMatchTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace EAFCMatchTracker.UnitTests;

public class PlayerCardServiceTests
{
    private static readonly DateTime T0 = AnalyticsSeed.Base; // 2026-09-01 12:00 UTC (09:00 em São Paulo)
    private static DateTime Day(int i) => T0.AddDays(i);

    private static PlayerCardService Svc(AnalyticsSeed seed, IMemoryCache? cache = null) =>
        new(seed.Db, cache ?? new MemoryCache(new MemoryCacheOptions()));

    private static Task<PlayerCardsDto> Cards(
        AnalyticsSeed seed, int min = 1, DateOnly? from = null, DateOnly? to = null, int? version = null) =>
        Svc(seed).GetCardsAsync(AnalyticsSeed.Club, from, to, version, min, default);

    private static async Task<PlayerCardDto> CardOf(AnalyticsSeed seed, long playerId, int min = 1) =>
        (await Cards(seed, min)).Cards.Single(c => c.PlayerEntityId == playerId);

    /// <summary>Altera a linha jogador/partida (chutes, passes, desarmes, defesas...) já semeada.</summary>
    private static void Edit(AnalyticsSeed seed, long matchId, long playerId, Action<MatchPlayerEntity> change)
    {
        change(seed.Db.MatchPlayers.Single(x => x.MatchId == matchId && x.PlayerEntityId == playerId));
        seed.Db.SaveChanges();
    }

    // ------------------------------------------------------------------ filtros

    [Fact]
    public async Task FiltersByGameVersionAndLocalDateRange()
    {
        using var seed = new AnalyticsSeed();
        seed.Match(Day(0), 1, 0, version: AnalyticsSeed.V26, ours: new[] { new Pl(11) });   // 01/09 FC26
        seed.Match(Day(4), 2, 0, version: AnalyticsSeed.V27, ours: new[] { new Pl(11) });   // 05/09
        seed.Match(Day(9), 0, 1, version: AnalyticsSeed.V27, ours: new[] { new Pl(11) });   // 10/09
        seed.Match(Day(19), 3, 0, version: AnalyticsSeed.V27, ours: new[] { new Pl(11) });  // 20/09

        var all = await Cards(seed);
        Assert.Equal(4, all.TotalMatches);
        Assert.Equal(4, all.Cards.Single().Matches);
        Assert.Equal("Meu Clube", all.ClubName);

        var v27 = await Cards(seed, version: 27);
        Assert.Equal((3, 3, (int?)27), (v27.TotalMatches, v27.Cards.Single().Matches, v27.GameVersion));
        Assert.Equal(1, (await Cards(seed, version: 26)).Cards.Single().Matches);

        // datas locais, inclusivas nas duas pontas
        var range = await Cards(seed, from: new DateOnly(2026, 9, 5), to: new DateOnly(2026, 9, 10));
        Assert.Equal(2, range.TotalMatches);
        Assert.Equal(2, range.Cards.Single().Matches);
        Assert.Equal(new DateOnly(2026, 9, 5), range.From);
        Assert.Equal(new DateOnly(2026, 9, 10), range.To);
        Assert.Equal(1, (await Cards(seed, from: new DateOnly(2026, 9, 20))).TotalMatches);
        Assert.Equal(1, (await Cards(seed, to: new DateOnly(2026, 9, 1))).TotalMatches);
    }

    [Fact]
    public async Task UnknownGameVersionIsAnEmptyResultNotAnError()
    {
        using var seed = new AnalyticsSeed();
        seed.Match(Day(0), 1, 0, ours: new[] { new Pl(11) });

        var r = await Cards(seed, version: 99);

        Assert.Equal(0, r.TotalMatches);
        Assert.Empty(r.Cards);
        Assert.Equal(99, r.GameVersion);
    }

    [Fact]
    public async Task EmptyClubReturnsNoCards()
    {
        using var seed = new AnalyticsSeed();
        var r = await Cards(seed);
        Assert.Equal(0, r.TotalMatches);
        Assert.Empty(r.Cards);
    }

    [Fact]
    public async Task MinMatchesFiltersAndIsClampedToOneThroughThirty()
    {
        using var seed = new AnalyticsSeed();
        for (var i = 0; i < 4; i++)
            seed.Match(Day(i), 1, 0, ours: i switch
            {
                0 => new[] { new Pl(11), new Pl(12), new Pl(13) },
                1 => new[] { new Pl(11), new Pl(12) },
                _ => new[] { new Pl(11) }
            });

        Assert.Equal(new long[] { 11 }, (await Cards(seed, min: 3)).Cards.Select(c => c.PlayerEntityId));
        Assert.Equal(new long[] { 11, 12 }, (await Cards(seed, min: 2)).Cards.Select(c => c.PlayerEntityId).OrderBy(x => x));

        var low = await Cards(seed, min: 0);
        Assert.Equal(1, low.MinMatches);
        Assert.Equal(3, low.Cards.Count);

        var high = await Cards(seed, min: 99);
        Assert.Equal(30, high.MinMatches);
        Assert.Empty(high.Cards);
    }

    [Fact]
    public async Task OnlyOurClubPlayersGetCardsAndDisconnectedRowsAreIgnored()
    {
        using var seed = new AnalyticsSeed();
        for (var i = 0; i < 3; i++)
            seed.Match(Day(i), 1, 0, ours: new[] { new Pl(11), new Pl(12) }, oppPlayers: 3);
        Edit(seed, 1, 12, mp => mp.Disconnected = true);

        var r = await Cards(seed);

        Assert.Equal(new long[] { 11, 12 }, r.Cards.Select(c => c.PlayerEntityId).OrderBy(x => x));
        Assert.Equal(2, r.Cards.Single(c => c.PlayerEntityId == 12).Matches); // a partida desconectada não conta
        Assert.Equal(3, r.TotalMatches);
    }

    // ------------------------------------------------------------------ provisória / amostra

    [Fact]
    public async Task ProvisionalBelowTenMatches()
    {
        using var seed = new AnalyticsSeed();
        for (var i = 0; i < 10; i++)
            seed.Match(Day(i), 1, 0, ours: i < 9 ? new[] { new Pl(21), new Pl(22) } : new[] { new Pl(21) });

        var r = await Cards(seed);

        var ten = r.Cards.Single(c => c.PlayerEntityId == 21);
        var nine = r.Cards.Single(c => c.PlayerEntityId == 22);
        Assert.Equal((10, false), (ten.Matches, ten.Provisional));
        Assert.Equal((9, true), (nine.Matches, nine.Provisional));
    }

    [Fact]
    public async Task SmallSamplesAreShrunkTowardFiftyAndAttendanceDrivesRegularity()
    {
        using var seed = new AnalyticsSeed();
        for (var i = 0; i < 4; i++)
            seed.Match(Day(i), 1, 0, ours: i < 2 ? new[] { new Pl(11), new Pl(12) } : new[] { new Pl(11) });

        var r = await Cards(seed);

        // Nota sempre 7,0 (desvio 0 => estabilidade 99).
        // A: presença 4/4 => raw 99, M=4 => (4x99 + 150)/7 = 78 => 50 + 1,4 x 28 = 89,2 -> 89.
        // B: 2/4 => raw 74,25, M=2 => (2x74,25 + 150)/5 = 59,7 => 50 + 1,4 x 9,7 = 63,6 -> 64.
        Assert.Equal(89, r.Cards.Single(c => c.PlayerEntityId == 11).Axes.Reg);
        Assert.Equal(64, r.Cards.Single(c => c.PlayerEntityId == 12).Axes.Reg);
    }

    // ------------------------------------------------------------------ estatísticas

    [Fact]
    public async Task StatsMatchTheExistingPlayerStatsAggregation()
    {
        using var seed = new AnalyticsSeed();
        var ids = new long[5];
        for (var i = 0; i < 5; i++)
            ids[i] = seed.Match(Day(i), 2, 1, ours: new[]
            {
                new Pl(11, Goals: i % 3, Assists: i % 2, Rating: 6.5 + i * 0.4, Mom: i == 2, Reds: i == 4 ? 1 : 0),
                new Pl(12, Goals: 1, Assists: 1, Rating: 7.3 - i * 0.2, Mom: i == 0)
            });
        for (var i = 0; i < 5; i++)
        {
            var k = i;
            Edit(seed, ids[i], 11, mp => { mp.Shots = (short)(3 + k); mp.Passattempts = (short)(20 + k * 3); mp.Passesmade = (short)(15 + k * 2); mp.Tackleattempts = (short)(4 + k); mp.Tacklesmade = (short)(2 + k % 3); });
            Edit(seed, ids[i], 12, mp => { mp.Shots = (short)(2 + k); mp.Passattempts = (short)(30 + k); mp.Passesmade = (short)(24 + k); mp.Tackleattempts = (short)(1 + k); mp.Tacklesmade = (short)(1 + k % 2); });
        }

        var cards = await Cards(seed);
        var reference = StatsAggregator.BuildPerPlayer(
            seed.Db.MatchPlayers.Include(mp => mp.Player).Where(mp => mp.ClubId == AnalyticsSeed.Club).ToList());

        Assert.Equal(2, reference.Count);
        foreach (var expected in reference)
        {
            var card = cards.Cards.Single(c => c.PlayerEntityId == expected.PlayerEntityId);
            Assert.Equal(expected.MatchesPlayed, card.Matches);
            Assert.Equal(expected.TotalGoals, card.Stats.Goals);
            Assert.Equal(expected.TotalAssists, card.Stats.Assists);
            Assert.Equal(expected.TotalMom, card.Stats.Motm);
            Assert.Equal(expected.TotalRedCards, card.Stats.RedCards);
            Assert.Equal(expected.GoalAccuracyPercent, card.Stats.ShotAccuracyPct, 2);
            Assert.Equal(expected.PassAccuracyPercent, card.Stats.PassAccuracyPct, 2);
            Assert.Equal(expected.TackleSuccessPercent, card.Stats.TackleAccuracyPct, 2);
            Assert.Equal(expected.AvgRating, card.Stats.AvgRating, 2);
            Assert.Equal(expected.TotalGoals / (double)expected.MatchesPlayed, card.Stats.GoalsPerMatch, 2);
            Assert.Equal(expected.TotalAssists / (double)expected.MatchesPlayed, card.Stats.AssistsPerMatch, 2);
        }
    }

    [Fact]
    public async Task AccuraciesAreZeroWithoutAttemptsAndMinutesComeFromSecondsPlayed()
    {
        using var seed = new AnalyticsSeed();
        for (var i = 0; i < 2; i++) seed.Match(Day(i), 1, 0, ours: new[] { new Pl(11, Goals: 1) });
        Edit(seed, 1, 11, mp => mp.SecondsPlayed = 5400);
        Edit(seed, 2, 11, mp => mp.SecondsPlayed = 2700);

        var c = await CardOf(seed, 11);

        Assert.Equal((0.0, 0.0, 0.0), (c.Stats.ShotAccuracyPct, c.Stats.PassAccuracyPct, c.Stats.TackleAccuracyPct));
        Assert.Equal(135, c.Minutes);
        Assert.Equal((2, 1.0), (c.Stats.Goals, c.Stats.GoalsPerMatch));
    }

    [Fact]
    public async Task PreAssistsComeFromGoalLinksAndAreZeroWithoutThem()
    {
        using var seed = new AnalyticsSeed();
        var m1 = seed.Match(Day(0), 2, 0, version: AnalyticsSeed.V26, ours: new[] { new Pl(11), new Pl(12), new Pl(13) });
        var m2 = seed.Match(Day(1), 1, 0, version: AnalyticsSeed.V27, ours: new[] { new Pl(11), new Pl(12), new Pl(13) });
        seed.Match(Day(2), 1, 0, version: AnalyticsSeed.V27, ours: new[] { new Pl(11), new Pl(12), new Pl(13) }); // sem links
        seed.Goal(m1, scorer: 11, assist: 12, pre: 13);
        seed.Goal(m1, scorer: 11, assist: 12, pre: 13);
        seed.Goal(m2, scorer: 12, assist: 11, pre: 13);
        seed.Goal(m2, scorer: 12, assist: null, pre: null);
        // gol de OUTRO clube não conta
        seed.Goal(m2, scorer: 12, assist: 11, pre: 13, clubId: AnalyticsSeed.Rival);

        var all = await Cards(seed);
        Assert.Equal(3, all.Cards.Single(c => c.PlayerEntityId == 13).Stats.PreAssists);
        Assert.Equal(0, all.Cards.Single(c => c.PlayerEntityId == 11).Stats.PreAssists);
        Assert.Equal(0, all.Cards.Single(c => c.PlayerEntityId == 12).Stats.PreAssists);

        // o filtro de edição restringe também os links
        var v27 = await Cards(seed, version: 27);
        Assert.Equal(1, v27.Cards.Single(c => c.PlayerEntityId == 13).Stats.PreAssists);

        // período só com a partida sem links: zero
        var none = await Cards(seed, from: new DateOnly(2026, 9, 3));
        Assert.Equal(0, none.Cards.Single(c => c.PlayerEntityId == 13).Stats.PreAssists);
    }

    [Fact]
    public async Task PreAssistsRaiseCreationAxis()
    {
        using var seed = new AnalyticsSeed();
        for (var i = 0; i < 3; i++)
        {
            var m = seed.Match(Day(i), 1, 0, ours: new[] { new Pl(11), new Pl(12) });
            seed.Goal(m, scorer: 12, assist: null, pre: 11);
        }

        var c = await CardOf(seed, 11);

        // 0 assistências + 0,5 x 1 pré-assistência por jogo = 0,5 => raw 0,5/0,8 x 99 = 61,875; M=3 => (3x61,875 + 150)/6 = 55,9 => 50 + 1,4 x 5,9 = 58,3 -> 58
        Assert.Equal(58, c.Axes.Cri);
        Assert.Equal(3, c.Stats.PreAssists);
    }

    // ------------------------------------------------------------------ goleiro

    [Fact]
    public async Task GoalkeeperGetsGolCleanSheetsAndSavePctWhileOutfieldersDoNot()
    {
        using var seed = new AnalyticsSeed();
        var gas = new[] { 0, 1, 0, 2 };
        var saves = new[] { 3, 4, 2, 3 };
        for (var i = 0; i < 4; i++)
        {
            var id = seed.Match(Day(i), 1, gas[i], ours: new[] { new Pl(30, Pos: "goalkeeper"), new Pl(31, Pos: "forward") });
            var s = saves[i];
            Edit(seed, id, 30, mp => mp.Saves = (short)s);
        }

        var gk = await CardOf(seed, 30);
        var fw = await CardOf(seed, 31);

        Assert.Equal("GOLEIRO", gk.PositionGroup);
        Assert.Null(gk.Axes.Ata);
        Assert.Equal(2, gk.Stats.CleanSheets);                     // jogos com 0 gols sofridos
        Assert.Equal(80.0, gk.Stats.SavePct);                      // 12 defesas / (12 + 3 gols sofridos)
        // GOL: .6 x scale(80, 40, 85) + .4 x scale(3 - 0,75, 0, 3) = 52,8 + 29,7 = 82,5; M=4 => (4x82,5 + 150)/7 = 68,6 => 50 + 1,4 x 18,6 = 76
        Assert.Equal(76, gk.Axes.Gol);

        Assert.Equal("ATAQUE", fw.PositionGroup);
        Assert.NotNull(fw.Axes.Ata);
        Assert.Null(fw.Axes.Gol);
        Assert.Null(fw.Stats.CleanSheets);
        Assert.Null(fw.Stats.SavePct);
    }

    [Fact]
    public async Task GoalkeeperWithoutSavesAndConcedingNothingHasZeroSavePctAndAllCleanSheets()
    {
        using var seed = new AnalyticsSeed();
        for (var i = 0; i < 3; i++) seed.Match(Day(i), 2, 0, ours: new[] { new Pl(30, Pos: "goalkeeper") });

        var gk = await CardOf(seed, 30);

        Assert.Equal((3, 0.0), (gk.Stats.CleanSheets, gk.Stats.SavePct));
        Assert.NotNull(gk.Axes.Gol);
    }

    // ------------------------------------------------------------------ posição, forma, atributos, ordem

    [Fact]
    public async Task PositionIsTheMostFrequentOneAndTiesGoToTheMostRecent()
    {
        using var seed = new AnalyticsSeed();
        // jogador 11: 3 x forward, 1 x midfielder (a mais recente) => forward
        for (var i = 0; i < 4; i++) seed.Match(Day(i), 1, 0, ours: new[] { new Pl(11, Pos: i == 3 ? "midfielder" : "forward") });
        // jogador 12: 2 x forward (antigas), 2 x midfielder (recentes) => empate => midfielder
        for (var i = 0; i < 4; i++) seed.Match(Day(10 + i), 1, 0, ours: new[] { new Pl(12, Pos: i < 2 ? "forward" : "midfielder") });

        var cards = await Cards(seed);

        Assert.Equal(("forward", "ATAQUE"), (cards.Cards.Single(c => c.PlayerEntityId == 11).Position, cards.Cards.Single(c => c.PlayerEntityId == 11).PositionGroup));
        Assert.Equal(("midfielder", "MEIO"), (cards.Cards.Single(c => c.PlayerEntityId == 12).Position, cards.Cards.Single(c => c.PlayerEntityId == 12).PositionGroup));
    }

    [Fact]
    public async Task UnknownPositionFallsBackToMidfield()
    {
        using var seed = new AnalyticsSeed();
        seed.Match(Day(0), 1, 0, ours: new[] { new Pl(11, Pos: "") });
        var c = await CardOf(seed, 11);
        Assert.Equal(("", "MEIO"), (c.Position, c.PositionGroup));
    }

    [Fact]
    public async Task FormIsTheLastFiveRatingsNewestFirstAndMayBeShorter()
    {
        using var seed = new AnalyticsSeed();
        var ratings = new[] { 6.0, 6.1, 6.2, 6.3, 6.4, 6.5, 6.6 };
        for (var i = 0; i < ratings.Length; i++)
            seed.Match(Day(i), 1, 0, ours: i == 6 ? new[] { new Pl(11, Rating: ratings[i]) } : new[] { new Pl(11, Rating: ratings[i]), new Pl(12, Rating: 8.0) });

        var cards = await Cards(seed);

        Assert.Equal(new[] { 6.6, 6.5, 6.4, 6.3, 6.2 }, cards.Cards.Single(c => c.PlayerEntityId == 11).Form);
        Assert.Equal(5, cards.Cards.Single(c => c.PlayerEntityId == 12).Form.Count); // 12 jogou 6 partidas: forma limitada a 5

        // período curto: menos de 5 partidas => forma mais curta
        var short3 = await Cards(seed, from: new DateOnly(2026, 9, 5));
        Assert.Equal(new[] { 6.6, 6.5, 6.4 }, short3.Cards.Single(c => c.PlayerEntityId == 11).Form);
    }

    [Fact]
    public async Task LastPlayedAtIsTheNewestMatchTimestamp()
    {
        using var seed = new AnalyticsSeed();
        seed.Match(Day(0), 1, 0, ours: new[] { new Pl(11) });
        seed.Match(Day(3), 1, 0, ours: new[] { new Pl(11) });
        Assert.Equal(Day(3), (await CardOf(seed, 11)).LastPlayedAt);
    }

    [Fact]
    public async Task AttributesComeFromTheLatestMatchThatHasThemAndAreNullOtherwise()
    {
        using var seed = new AnalyticsSeed();
        var ids = new long[3];
        for (var i = 0; i < 3; i++) ids[i] = seed.Match(Day(i), 1, 0, ours: new[] { new Pl(11), new Pl(12) });
        Edit(seed, ids[0], 11, mp => { mp.ProOverall = 70; mp.ProHeight = 180; });
        Edit(seed, ids[1], 11, mp => { mp.ProOverall = 75; mp.ProHeight = 181; });
        // a partida mais nova não traz atributos: vale a última que traz
        var cards = await Cards(seed);

        var a = cards.Cards.Single(c => c.PlayerEntityId == 11).Attributes;
        Assert.NotNull(a);
        Assert.Equal((75, "181 cm"), (a!.Overall, a.Height));
        Assert.Null(cards.Cards.Single(c => c.PlayerEntityId == 12).Attributes);

        // só a versão em texto
        Edit(seed, ids[2], 12, mp => mp.ProOverallStr = "82");
        var b = (await CardOf(seed, 12)).Attributes;
        Assert.Equal((82, (string?)null), (b!.Overall, b.Height));
    }

    [Fact]
    public async Task CardsAreOrderedByOverallThenMatchesThenName()
    {
        using var seed = new AnalyticsSeed();
        for (var i = 0; i < 5; i++)
            seed.Match(Day(i), 3, 0, ours: new[]
            {
                new Pl(11, Goals: 2, Assists: 1, Rating: 9.0, Mom: true),
                new Pl(12, Goals: 0, Rating: 5.5),
                new Pl(13, Goals: 0, Rating: 5.5)
            });

        var cards = (await Cards(seed)).Cards;

        Assert.Equal(11, cards[0].PlayerEntityId);
        Assert.True(cards[0].Overall > cards[1].Overall);
        Assert.Equal(cards.OrderByDescending(c => c.Overall).ThenByDescending(c => c.Matches).ThenBy(c => c.Name).Select(c => c.PlayerEntityId),
            cards.Select(c => c.PlayerEntityId));
        Assert.Equal(cards[1].Overall, cards[2].Overall);
        Assert.True(string.CompareOrdinal(cards[1].Name, cards[2].Name) < 0); // empate total: por nome
    }

    [Fact]
    public void OrderCardsBreaksOverallTiesWithMoreMatches()
    {
        PlayerCardDto C(long id, int overall, int matches) => new() { PlayerEntityId = id, Name = $"P{id}", Overall = overall, Matches = matches };

        var ordered = PlayerCardService.OrderCards(new[] { C(1, 70, 5), C(2, 70, 9), C(3, 80, 1), C(4, 70, 9) });

        Assert.Equal(new long[] { 3, 2, 4, 1 }, ordered.Select(c => c.PlayerEntityId));
    }

    [Fact]
    public async Task TierFollowsOverallThresholds()
    {
        using var seed = new AnalyticsSeed();
        for (var i = 0; i < 12; i++)
            seed.Match(Day(i), 3, 0, ours: new[] { new Pl(11, Goals: 3, Assists: 2, Rating: 9.8, Mom: true), new Pl(12, Rating: 4.0) });

        var cards = await Cards(seed);

        foreach (var c in cards.Cards)
            Assert.Equal(CardScoring.Tier(c.Overall), c.Tier);
        Assert.Equal("bronze", cards.Cards.Single(c => c.PlayerEntityId == 12).Tier);
        Assert.InRange(cards.Cards.Single(c => c.PlayerEntityId == 11).Overall, 1, 99);
    }

    // ------------------------------------------------------------------ cache

    [Fact]
    public async Task CacheKeyIncludesTheClubSessionSettings()
    {
        using var seed = new AnalyticsSeed();
        for (var i = 0; i < 3; i++) seed.Match(Day(i), 1, 0, ours: new[] { new Pl(11, Rating: 7.0) });
        var cache = new MemoryCache(new MemoryCacheOptions());
        var svc = new PlayerCardService(seed.Db, cache);

        Task<double> AvgAsync() => svc.GetCardsAsync(AnalyticsSeed.Club, null, null, null, 1, default)
            .ContinueWith(t => t.Result.Cards.Single().Stats.AvgRating);

        Assert.Equal(7.0, await AvgAsync());

        // Muda os dados SEM partida/link novos: o fingerprint não muda, então o cache de 60 s ainda responde (valor antigo).
        foreach (var mp in seed.Db.MatchPlayers) mp.Rating = 8.0;
        seed.Db.SaveChanges();
        Assert.Equal(7.0, await AvgAsync());

        // Mudar a parametrização de sessão do admin muda o fingerprint => a chave de cache muda => recalcula.
        seed.Db.TrackedClubs.Find(AnalyticsSeed.Club)!.SessionGapMinutes = 45;
        seed.Db.SaveChanges();
        Assert.Equal(8.0, await AvgAsync());

        // e o fuso também
        foreach (var mp in seed.Db.MatchPlayers) mp.Rating = 6.0;
        seed.Db.SaveChanges();
        Assert.Equal(8.0, await AvgAsync());
        seed.Db.TrackedClubs.Find(AnalyticsSeed.Club)!.TimeZoneId = "UTC";
        seed.Db.SaveChanges();
        Assert.Equal(6.0, await AvgAsync());

        // fronteira manual de sessão também
        foreach (var mp in seed.Db.MatchPlayers) mp.Rating = 9.0;
        seed.Db.SaveChanges();
        Assert.Equal(6.0, await AvgAsync());
        seed.Db.SessionBoundaries.Add(new SessionBoundaryEntity { ClubId = AnalyticsSeed.Club, MatchId = 2, StartNewSession = true });
        seed.Db.SaveChanges();
        Assert.Equal(9.0, await AvgAsync());
    }

    // ------------------------------------------------------------------ comparador

    private static Task<PlayerCompareDto?> Compare(
        AnalyticsSeed seed, long a, long b, DateOnly? from = null, DateOnly? to = null, int? version = null, IMemoryCache? cache = null) =>
        Svc(seed, cache).GetCompareAsync(AnalyticsSeed.Club, a, b, from, to, version, default);

    [Fact]
    public async Task CompareReturnsNullForPlayersOutsideTheClub()
    {
        using var seed = new AnalyticsSeed();
        seed.Match(Day(0), 1, 0, ours: new[] { new Pl(11), new Pl(12) });
        seed.Player(900, "De outro clube", AnalyticsSeed.Rival);
        seed.Db.MatchPlayers.Add(new MatchPlayerEntity
        {
            MatchId = 1, ClubId = AnalyticsSeed.Rival, PlayerEntityId = 900, Pos = "midfielder", Realtimegame = "", Realtimeidle = "",
            Vproattr = "", Vprohackreason = "", MatchEventAggregate0 = "", MatchEventAggregate1 = "", MatchEventAggregate2 = "", MatchEventAggregate3 = ""
        });
        seed.Db.SaveChanges();

        Assert.NotNull(await Compare(seed, 11, 12));
        Assert.Null(await Compare(seed, 11, 777));      // não existe
        Assert.Null(await Compare(seed, 777, 11));
        Assert.Null(await Compare(seed, 11, 900));      // existe, mas é de outro clube
        Assert.Null(await Compare(seed, 900, 12));
    }

    [Fact]
    public async Task CompareBuildsBothCardsWithMinMatchesOne()
    {
        using var seed = new AnalyticsSeed();
        for (var i = 0; i < 5; i++)
            seed.Match(Day(i), 1, 0, ours: i == 0 ? new[] { new Pl(11), new Pl(12) } : new[] { new Pl(11) });

        var r = (await Compare(seed, 11, 12))!;

        Assert.Equal((AnalyticsSeed.Club, 11L, 12L), (r.ClubId, r.A.PlayerEntityId, r.B.PlayerEntityId));
        Assert.Equal((5, 1), (r.A.Matches, r.B.Matches));   // B tem 1 jogo e mesmo assim ganha carta (provisória)
        Assert.True(r.B.Provisional);
    }

    [Fact]
    public async Task CompareMetricsPickTheRightWinnerIncludingLowerIsBetterAndTies()
    {
        using var seed = new AnalyticsSeed();
        for (var i = 0; i < 4; i++)
            seed.Match(Day(i), 2, 1, ours: new[]
            {
                new Pl(11, Goals: 1, Assists: 0, Rating: 8.0, Reds: i == 0 ? 1 : 0),
                new Pl(12, Goals: 0, Assists: 1, Rating: 7.0)
            });

        var r = (await Compare(seed, 11, 12))!;
        var m = r.Metrics.ToDictionary(x => x.Key);

        Assert.Equal(new[] { "overall", "ata", "pas", "cri", "def", "imp", "reg", "goalsPerMatch", "assistsPerMatch", "avgRating",
                "shotAccuracyPct", "passAccuracyPct", "tackleAccuracyPct", "motm", "redCards" },
            r.Metrics.Select(x => x.Key));
        Assert.All(r.Metrics, x => Assert.False(string.IsNullOrWhiteSpace(x.Label)));

        Assert.Equal((1.0, 0.0, "a", true), (m["goalsPerMatch"].A!.Value, m["goalsPerMatch"].B!.Value, m["goalsPerMatch"].Winner, m["goalsPerMatch"].HigherIsBetter));
        Assert.Equal("b", m["assistsPerMatch"].Winner);
        Assert.Equal("a", m["avgRating"].Winner);
        Assert.Equal("a", m["imp"].Winner);
        // menor é melhor: A levou 1 vermelho, B nenhum => B vence
        Assert.Equal((1.0, 0.0, "b", false), (m["redCards"].A!.Value, m["redCards"].B!.Value, m["redCards"].Winner, m["redCards"].HigherIsBetter));
        // ninguém foi o melhor em campo => empate
        Assert.Equal("tie", m["motm"].Winner);
        // sem chutes/passes/desarmes registrados os dois têm 0% => empate
        Assert.Equal("tie", m["passAccuracyPct"].Winner);
        // overall: o vencedor é o de maior overall
        Assert.Equal(r.A.Overall > r.B.Overall ? "a" : r.A.Overall < r.B.Overall ? "b" : "tie", m["overall"].Winner);
        Assert.Equal((double)r.A.Overall, m["overall"].A);
    }

    [Fact]
    public async Task CompareRedCardsTieWhenNeitherOrBothHaveThem()
    {
        using var seed = new AnalyticsSeed();
        for (var i = 0; i < 2; i++)
            seed.Match(Day(i), 1, 0, ours: new[] { new Pl(11, Reds: 1), new Pl(12, Reds: 1) });
        var tie = (await Compare(seed, 11, 12))!.Metrics.Single(x => x.Key == "redCards");
        Assert.Equal("tie", tie.Winner);

        Edit(seed, 1, 12, mp => mp.Redcards = 3);
        // outra instância de cache: o resultado anterior está em cache só no serviço antigo
        var moreForB = (await Compare(seed, 11, 12))!.Metrics.Single(x => x.Key == "redCards");
        Assert.Equal(("a", 2.0, 4.0), (moreForB.Winner, moreForB.A!.Value, moreForB.B!.Value)); // A levou 2, B levou 4 => A (menos) vence
    }

    [Fact]
    public async Task CompareGoalkeeperVersusOutfielderHasNullAxesAndNoWinnerForThem()
    {
        using var seed = new AnalyticsSeed();
        for (var i = 0; i < 3; i++)
            seed.Match(Day(i), 1, 0, ours: new[] { new Pl(11, Pos: "forward", Goals: 1), new Pl(30, Pos: "goalkeeper") });

        var r = (await Compare(seed, 11, 30))!;
        var m = r.Metrics.ToDictionary(x => x.Key);

        Assert.NotNull(m["ata"].A);
        Assert.Null(m["ata"].B);
        Assert.Null(m["ata"].Winner);
        Assert.Null(m["gol"].A);
        Assert.NotNull(m["gol"].B);
        Assert.Null(m["gol"].Winner);
        Assert.NotNull(m["overall"].Winner);
        Assert.NotNull(m["pas"].Winner);
    }

    [Fact]
    public async Task CompareTwoGoalkeepersShowsGolAndNoAta()
    {
        using var seed = new AnalyticsSeed();
        for (var i = 0; i < 3; i++)
        {
            var id = seed.Match(Day(i), 1, 1, ours: new[] { new Pl(30, Pos: "goalkeeper"), new Pl(31, Pos: "goalkeeper") });
            Edit(seed, id, 30, mp => mp.Saves = 5);
            Edit(seed, id, 31, mp => mp.Saves = 1);
        }

        var r = (await Compare(seed, 30, 31))!;

        Assert.DoesNotContain(r.Metrics, x => x.Key == "ata");
        var gol = r.Metrics.Single(x => x.Key == "gol");
        Assert.NotNull(gol.A);
        Assert.Equal("a", gol.Winner); // mais defesas
    }

    [Fact]
    public async Task CompareTogetherOnlyAAndOnlyBMath()
    {
        using var seed = new AnalyticsSeed();
        seed.Match(Day(0), 2, 0, ours: new[] { new Pl(11), new Pl(12) });             // juntos, vitória 2-0
        seed.Match(Day(1), 0, 1, ours: new[] { new Pl(11), new Pl(12) });             // juntos, derrota 0-1
        seed.Match(Day(2), 3, 1, ours: new[] { new Pl(11), new Pl(13) });             // só A, vitória 3-1
        seed.Match(Day(3), 1, 1, ours: new[] { new Pl(12), new Pl(13) });             // só B, empate 1-1
        seed.Match(Day(4), 5, 0, ours: new[] { new Pl(13) });                         // nenhum dos dois

        var r = (await Compare(seed, 11, 12))!;

        var t = r.Together!;
        Assert.Equal((2, 1, 0, 1), (t.Matches, t.Wins, t.Draws, t.Losses));
        Assert.Equal((50.0, 1.5, 1.0, 0.5), (t.WinRatePct, t.PointsPerMatch, t.GoalsForPerMatch, t.GoalsAgainstPerMatch));

        var a = r.OnlyA!;
        Assert.Equal((1, 1, 0, 0), (a.Matches, a.Wins, a.Draws, a.Losses));
        Assert.Equal((100.0, 3.0, 3.0, 1.0), (a.WinRatePct, a.PointsPerMatch, a.GoalsForPerMatch, a.GoalsAgainstPerMatch));

        var b = r.OnlyB!;
        Assert.Equal((1, 0, 1, 0), (b.Matches, b.Wins, b.Draws, b.Losses));
        Assert.Equal((0.0, 1.0, 1.0, 1.0), (b.WinRatePct, b.PointsPerMatch, b.GoalsForPerMatch, b.GoalsAgainstPerMatch));
    }

    [Fact]
    public async Task CompareBlocksAreNullWhenThereAreNoSuchMatches()
    {
        using var seed = new AnalyticsSeed();
        seed.Match(Day(0), 1, 0, ours: new[] { new Pl(11) });
        seed.Match(Day(1), 1, 0, ours: new[] { new Pl(12) });

        var r = (await Compare(seed, 11, 12))!;

        Assert.Null(r.Together);                 // nunca jogaram juntos
        Assert.Equal(1, r.OnlyA!.Matches);
        Assert.Equal(1, r.OnlyB!.Matches);

        // quando um dos dois jogou todas as partidas do outro, "só A" some
        using var seed2 = new AnalyticsSeed();
        seed2.Match(Day(0), 1, 0, ours: new[] { new Pl(11), new Pl(12) });
        seed2.Match(Day(1), 1, 0, ours: new[] { new Pl(12) });
        var r2 = (await Compare(seed2, 11, 12))!;
        Assert.Null(r2.OnlyA);
        Assert.Equal(1, r2.Together!.Matches);
        Assert.Equal(1, r2.OnlyB!.Matches);
    }

    [Fact]
    public async Task CompareRatingSeriesHasTheLastTwentyMatchesInAscendingOrder()
    {
        using var seed = new AnalyticsSeed();
        var ids = new List<long>();
        for (var i = 0; i < 25; i++)
            ids.Add(seed.Match(Day(i), 1, 0, ours: i % 2 == 0
                ? new[] { new Pl(11, Rating: 6.0 + i * 0.1) }
                : new[] { new Pl(11, Rating: 6.0 + i * 0.1), new Pl(12, Rating: 5.0 + i * 0.1) }));
        ids.Add(seed.Match(Day(30), 1, 0, ours: new[] { new Pl(13) })); // nenhum dos dois jogou: fora da série

        var r = (await Compare(seed, 11, 12))!;

        Assert.Equal(20, r.RatingSeries.Count);
        Assert.Equal(ids.Skip(5).Take(20), r.RatingSeries.Select(p => p.MatchId));            // as 20 últimas em que alguém jogou
        Assert.Equal(r.RatingSeries.OrderBy(p => p.Timestamp).Select(p => p.MatchId), r.RatingSeries.Select(p => p.MatchId));
        var first = r.RatingSeries[0];                                                         // partida índice 5 (ímpar): os dois jogaram
        Assert.Equal((6.5, 5.5), (first.A!.Value, first.B!.Value));
        var second = r.RatingSeries[1];                                                        // índice 6 (par): só A
        Assert.Equal(6.6, second.A!.Value, 6);
        Assert.Null(second.B);
        Assert.Equal(Day(5), first.Timestamp);
    }

    [Fact]
    public async Task CompareRatingSeriesIsShorterWithFewMatches()
    {
        using var seed = new AnalyticsSeed();
        seed.Match(Day(0), 1, 0, ours: new[] { new Pl(11, Rating: 7.0), new Pl(12, Rating: 6.0) });
        seed.Match(Day(1), 1, 0, ours: new[] { new Pl(12, Rating: 8.0) });

        var r = (await Compare(seed, 11, 12))!;

        Assert.Equal(2, r.RatingSeries.Count);
        Assert.Equal((7.0, 6.0), (r.RatingSeries[0].A!.Value, r.RatingSeries[0].B!.Value));
        Assert.Null(r.RatingSeries[1].A);
        Assert.Equal(8.0, r.RatingSeries[1].B);
    }

    [Fact]
    public async Task ComparePlayerWithoutMatchesInThePeriodGetsAnEmptyCardAndNullMetrics()
    {
        using var seed = new AnalyticsSeed();
        seed.Match(Day(0), 1, 0, version: AnalyticsSeed.V26, ours: new[] { new Pl(11) });
        seed.Match(Day(1), 1, 0, version: AnalyticsSeed.V27, ours: new[] { new Pl(11), new Pl(12) });

        var r = (await Compare(seed, 11, 12, version: 26))!;

        Assert.Equal((1, 0), (r.A.Matches, r.B.Matches));
        Assert.Equal("Jogador 12", r.B.Name);
        Assert.Equal(0, r.B.Overall);
        Assert.All(r.Metrics, m => { Assert.Null(m.B); Assert.Null(m.Winner); });
        Assert.NotNull(r.Metrics[0].A);
    }

    [Fact]
    public async Task CompareHonoursFiltersAndSwappingAAndBGivesDifferentCachedResults()
    {
        using var seed = new AnalyticsSeed();
        for (var i = 0; i < 3; i++)
            seed.Match(Day(i), 1, 0, version: i == 0 ? AnalyticsSeed.V26 : AnalyticsSeed.V27, ours: new[] { new Pl(11, Goals: 1), new Pl(12) });
        var cache = new MemoryCache(new MemoryCacheOptions());

        var ab = (await Compare(seed, 11, 12, cache: cache))!;
        var ba = (await Compare(seed, 12, 11, cache: cache))!;
        var v27 = (await Compare(seed, 11, 12, version: 27, cache: cache))!;

        Assert.Equal((11L, 12L), (ab.A.PlayerEntityId, ab.B.PlayerEntityId));
        Assert.Equal((12L, 11L), (ba.A.PlayerEntityId, ba.B.PlayerEntityId));
        Assert.Equal((3, 2), (ab.A.Matches, v27.A.Matches));
    }

    [Theory]
    [InlineData(1.0, 2.0, true, "b")]
    [InlineData(2.0, 1.0, true, "a")]
    [InlineData(1.0, 2.0, false, "a")]
    [InlineData(2.0, 1.0, false, "b")]
    [InlineData(3.0, 3.0, true, "tie")]
    [InlineData(3.0, 3.0, false, "tie")]
    [InlineData(null, 3.0, true, null)]
    [InlineData(3.0, null, false, null)]
    [InlineData(null, null, true, null)]
    public void WinnerLogic(double? a, double? b, bool higherIsBetter, string? expected) =>
        Assert.Equal(expected, PlayerCardService.Winner(a, b, higherIsBetter));
}
