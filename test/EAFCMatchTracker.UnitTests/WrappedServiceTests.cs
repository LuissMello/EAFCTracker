using EAFCMatchTracker.Application.Dtos;
using EAFCMatchTracker.Application.Services.Analytics;
using Xunit;

namespace EAFCMatchTracker.UnitTests;

public class WrappedServiceTests
{
    private static readonly DateTime T0 = new(2026, 9, 1, 15, 0, 0, DateTimeKind.Utc); // 12:00 em São Paulo
    private static DateTime Day(int i) => T0.AddDays(i);

    private static MatchRow Row(int gf, int ga, int day = 0) => new() { MatchId = day, Timestamp = Day(day), Gf = gf, Ga = ga };

    // ------------------------------------------------------------------ streaks

    [Fact]
    public void StreaksCountConsecutiveMatchesAndKeepTheEarliestOnTies()
    {
        //                        W     W     D     W     W     W     L     L     D     L
        var rows = new[] { (1, 0), (2, 0), (1, 1), (3, 1), (1, 0), (2, 1), (0, 1), (0, 2), (1, 1), (0, 3) }
            .Select((s, i) => Row(s.Item1, s.Item2, i)).ToList();
        DateOnly Local(MatchRow m) => DateOnly.FromDateTime(m.Timestamp);

        var win = WrappedService.Longest(rows, m => m.Gf > m.Ga, Local)!;
        Assert.Equal((3, new DateOnly(2026, 9, 4), new DateOnly(2026, 9, 6)), (win.Length, win.From, win.To));
        var unbeaten = WrappedService.Longest(rows, m => m.Gf >= m.Ga, Local)!;
        Assert.Equal((6, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 6)), (unbeaten.Length, unbeaten.From, unbeaten.To));
        var winless = WrappedService.Longest(rows, m => m.Gf <= m.Ga, Local)!;
        Assert.Equal((4, new DateOnly(2026, 9, 7), new DateOnly(2026, 9, 10)), (winless.Length, winless.From, winless.To));
        var clean = WrappedService.Longest(rows, m => m.Ga == 0, Local)!;
        Assert.Equal((2, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 2)), (clean.Length, clean.From, clean.To)); // empate com 1-0 (idx 4): fica a mais antiga
        Assert.Null(WrappedService.Longest(rows.Take(1).Select(r => Row(0, 1)).ToList(), m => m.Gf > m.Ga, Local));
    }

    [Fact]
    public async Task StreakDatesAreLocalDates()
    {
        using var seed = new AnalyticsSeed();
        // 02/10 02:00 UTC = 01/10 23:00 em São Paulo
        seed.Match(new DateTime(2026, 10, 2, 2, 0, 0, DateTimeKind.Utc), 1, 0);

        var w = await seed.Wrapped().GetWrappedAsync(AnalyticsSeed.Club, null, default);

        Assert.Equal(new DateOnly(2026, 10, 1), w.Streaks.LongestWin!.From);
        Assert.Equal(new DateOnly(2026, 10, 1), w.From);
        Assert.Equal(new DateOnly(2026, 10, 1), w.To);
    }

    // ------------------------------------------------------------------ totals / sessions / moments

    [Fact]
    public async Task TotalsSessionsAndBestWorstSessionsRequireThreeMatches()
    {
        using var seed = new AnalyticsSeed();
        // A: 3 vitórias; B: 3 derrotas; C: 2 vitórias (perfeita, mas só 2 partidas => inelegível).
        var a = seed.Match(Day(0), 1, 0);
        seed.Match(Day(0).AddMinutes(30), 2, 0);
        seed.Match(Day(0).AddMinutes(60), 3, 0);
        var b = seed.Match(Day(1), 0, 1);
        seed.Match(Day(1).AddMinutes(30), 0, 2);
        seed.Match(Day(1).AddMinutes(60), 0, 3);
        seed.Match(Day(2), 5, 0);
        seed.Match(Day(2).AddMinutes(30), 4, 0);

        var w = await seed.Wrapped().GetWrappedAsync(AnalyticsSeed.Club, null, default);

        Assert.Equal((8, 5, 0, 3), (w.Totals.Matches, w.Totals.Wins, w.Totals.Draws, w.Totals.Losses));
        Assert.Equal(62.5, w.Totals.WinRatePct);
        Assert.Equal((15, 6, 5), (w.Totals.GoalsFor, w.Totals.GoalsAgainst, w.Totals.CleanSheets));
        Assert.Equal((3, 3), (w.Totals.Sessions, w.Totals.ActiveDays));
        Assert.Equal(60 + 60 + 30 + 3 * 15, w.Totals.EstimatedMinutes);

        Assert.Equal(a, w.BigMoments.BestSession!.SessionId);
        Assert.Equal((3, 0, 0), (w.BigMoments.BestSession.Wins, w.BigMoments.BestSession.Draws, w.BigMoments.BestSession.Losses));
        Assert.Equal(b, w.BigMoments.WorstSession!.SessionId);
        Assert.Equal(new DateOnly(2026, 9, 2), w.BigMoments.WorstSession.Date);

        Assert.Equal((5, 0), (w.BigMoments.BiggestWin!.GoalsFor, w.BigMoments.BiggestWin.GoalsAgainst));
        Assert.Equal((0, 3), (w.BigMoments.WorstLoss!.GoalsFor, w.BigMoments.WorstLoss.GoalsAgainst));
        Assert.Equal(5, w.BigMoments.HighestScoring!.GoalsFor + w.BigMoments.HighestScoring.GoalsAgainst);
    }

    [Fact]
    public async Task WithOnlyOneEligibleSessionThereIsNoWorstSession()
    {
        using var seed = new AnalyticsSeed();
        seed.Match(Day(0), 1, 0);
        seed.Match(Day(0).AddMinutes(30), 1, 0);
        seed.Match(Day(0).AddMinutes(60), 0, 1);
        seed.Match(Day(1), 0, 3); // 1 partida: inelegível

        var w = await seed.Wrapped().GetWrappedAsync(AnalyticsSeed.Club, null, default);

        Assert.NotNull(w.BigMoments.BestSession);
        Assert.Null(w.BigMoments.WorstSession);
    }

    // ------------------------------------------------------------------ opponents

    [Fact]
    public async Task VictimAndNemesisNeedThreeMatchesAndAClearBalance()
    {
        using var seed = new AnalyticsSeed();
        var day = 0;
        void Add(long opp, string name, int gf, int ga) => seed.Match(Day(day++), gf, ga, oppId: opp, oppName: name);
        for (var i = 0; i < 3; i++) Add(201, "Vítima FC", 2, 0);
        for (var i = 0; i < 3; i++) Add(202, "Carrasco FC", 0, 2);
        for (var i = 0; i < 2; i++) Add(203, "Poucos FC", 3, 0);   // só 2 jogos: inelegível
        Add(204, "Equilibrado FC", 1, 0); Add(204, "Equilibrado FC", 1, 1); Add(204, "Equilibrado FC", 0, 1); // 3 jogos, V=D

        var w = await seed.Wrapped().GetWrappedAsync(AnalyticsSeed.Club, null, default);

        Assert.Equal("Vítima FC", w.Opponents.FavoriteVictim!.Name);
        Assert.Equal((3, 3, 0, 0), (w.Opponents.FavoriteVictim.Matches, w.Opponents.FavoriteVictim.Wins, w.Opponents.FavoriteVictim.Draws, w.Opponents.FavoriteVictim.Losses));
        Assert.Equal("Carrasco FC", w.Opponents.Nemesis!.Name);
        Assert.Equal(3, w.Opponents.Nemesis.Losses);
        Assert.Equal("Vítima FC", w.Opponents.MostFaced!.Name); // 3 jogos como os outros; mais vitórias desempata
        Assert.Equal("45", w.Opponents.FavoriteVictim.CrestAssetId);
    }

    [Fact]
    public async Task NoOpponentQualifiesBelowThreeMatches()
    {
        using var seed = new AnalyticsSeed();
        seed.Match(Day(0), 2, 0);
        seed.Match(Day(1), 0, 2);

        var w = await seed.Wrapped().GetWrappedAsync(AnalyticsSeed.Club, null, default);

        Assert.NotNull(w.Opponents.MostFaced);
        Assert.Null(w.Opponents.FavoriteVictim);
        Assert.Null(w.Opponents.Nemesis);
    }

    // ------------------------------------------------------------------ players / duo

    [Fact]
    public async Task BestDuoComesFromAssistToGoalLinksWithAtLeastTwoGoals()
    {
        using var seed = new AnalyticsSeed();
        var ids = new List<long>();
        for (var i = 0; i < 6; i++)
            ids.Add(seed.Match(Day(i), 2, 0, ours: new[] { new Pl(11, Assists: 1), new Pl(12, Goals: 1), new Pl(14, Goals: 3, Mom: true, Reds: 1) }));
        seed.Goal(ids[0], 12, 11);
        seed.Goal(ids[1], 12, 11);
        seed.Goal(ids[2], 12, 11);
        seed.Goal(ids[3], 11, 12); // inverso: só 1
        seed.Goal(ids[4], 14, 14); // autoassistência não conta
        seed.Goal(ids[4], 14, 14);
        seed.Goal(ids[4], 14, 14);
        seed.Goal(ids[5], 14);

        var w = await seed.Wrapped().GetWrappedAsync(AnalyticsSeed.Club, null, default);

        Assert.Equal(("Jogador 12", "Jogador 11", 3), (w.BestDuo!.ScorerName, w.BestDuo.AssisterName, w.BestDuo.Goals));
        Assert.Equal("Jogador 14", w.Players.TopScorer!.Name);
        Assert.Equal(18, w.Players.TopScorer.Value);
        Assert.Equal("Jogador 11", w.Players.TopAssister!.Name);
        Assert.Equal(6, w.Players.TopAssister.Value);
        Assert.Equal(6, w.Players.HatTricks);
        Assert.Equal(("Jogador 14", 6.0), (w.Players.MostRedCards!.Name, w.Players.MostRedCards.Value));
        Assert.Equal(6.0, w.Players.MostMotm!.Value);
        Assert.Null(w.Players.BestAvgRating); // ninguém com 10 partidas
        Assert.Equal(6.0, w.Players.MostMatches!.Value);
    }

    [Fact]
    public async Task BestDuoIsNullWithASingleLinkedGoal()
    {
        using var seed = new AnalyticsSeed();
        var m = seed.Match(Day(0), 1, 0, ours: new[] { new Pl(11), new Pl(12, Goals: 1) });
        seed.Goal(m, 12, 11);

        var w = await seed.Wrapped().GetWrappedAsync(AnalyticsSeed.Club, null, default);

        Assert.Null(w.BestDuo);
    }

    // ------------------------------------------------------------------ progression

    private static AnalyticsSeed LongSrSeed(int matches)
    {
        var s = new AnalyticsSeed();
        for (var i = 0; i < matches; i++)
        {
            var sr = i == 150 ? 1700 : i == 40 ? 1300 : 1500 + (i % 7);
            s.Match(Day(i / 3).AddMinutes((i % 3) * 20), i % 2, 0, ourSr: sr, division: 5, ours: new[] { new Pl(11, Rating: 7.5) });
        }
        return s;
    }

    [Fact]
    public async Task SrSeriesIsDownsampledToAtMost120PointsKeepingEndsPeakAndLow()
    {
        using var seed = LongSrSeed(300);

        var w = await seed.Wrapped().GetWrappedAsync(AnalyticsSeed.Club, null, default);

        var sr = w.Progression.SkillRating;
        Assert.Equal((1500, 1500 + 299 % 7), (sr.Start, sr.End));
        Assert.Equal(1700, sr.Peak!.Value);
        Assert.Equal(1300, sr.Low!.Value);
        Assert.InRange(w.Progression.SrSeries.Count, 3, WrappedService.MaxSrPoints);
        Assert.Equal(1500, w.Progression.SrSeries[0].Value);
        Assert.Equal(sr.End, w.Progression.SrSeries[^1].Value);
        Assert.Contains(w.Progression.SrSeries, p => p.Value == 1700);
        Assert.Contains(w.Progression.SrSeries, p => p.Value == 1300);
        Assert.True(w.Progression.SrSeries.Zip(w.Progression.SrSeries.Skip(1), (a, b) => a.Date <= b.Date).All(x => x));
        Assert.Equal((5, 5), (w.Progression.Division.Start, w.Progression.Division.End));
        Assert.Equal(7.5, w.Players.BestAvgRating!.Value); // 300 partidas >= 10
        Assert.Equal(300, w.Players.BestAvgRating.Matches);
    }

    [Fact]
    public async Task ShortSeriesIsNotDownsampled()
    {
        using var seed = LongSrSeed(30);
        var w = await seed.Wrapped().GetWrappedAsync(AnalyticsSeed.Club, null, default);
        Assert.Equal(30, w.Progression.SrSeries.Count);
    }

    [Theory]
    [InlineData(121)]
    [InlineData(500)]
    [InlineData(5000)]
    public void DownsampleNeverExceedsTheLimit(int n)
    {
        var points = Enumerable.Range(0, n).Select(i => new WrappedSrPointDto { Date = new DateOnly(2026, 1, 1).AddDays(i % 300), Value = i }).ToList();

        var result = WrappedService.Downsample(points, peakIdx: n / 2, lowIdx: n / 3, WrappedService.MaxSrPoints);

        Assert.InRange(result.Count, 1, WrappedService.MaxSrPoints);
        Assert.Equal(0, result[0].Value);
        Assert.Equal(n - 1, result[^1].Value);
        Assert.Contains(result, p => p.Value == n / 2);
        Assert.Contains(result, p => p.Value == n / 3);
        Assert.Equal(result.Select(p => p.Value).OrderBy(v => v), result.Select(p => p.Value)); // ordem preservada
    }

    [Fact]
    public void PromotionsAndRelegationsUseCounterIncreasesThenDivisionChanges()
    {
        MatchRow R(int? prom, int? rel) => new() { OurPromotions = prom, OurRelegations = rel };
        // contadores acumulados: 1,1,2,0 (reinício ignorado),1 -> promoções 1+0+... = (1->1:0)+(1->2:1)+(2->0:0)+(0->1:1) = 2
        var withCounters = new List<MatchRow> { R(1, 0), R(1, 0), R(2, 1), R(0, 1), R(1, 1) };
        Assert.Equal((2, 1), WrappedService.CountMovements(withCounters, new List<int>()));

        // sem contadores: número de divisão menor = promoção
        var noCounters = new List<MatchRow> { R(null, null), R(null, null), R(null, null), R(null, null) };
        Assert.Equal((1, 1), WrappedService.CountMovements(noCounters, new List<int> { 5, 4, 4, 5 }));
    }

    // ------------------------------------------------------------------ versions / empty / facts

    [Fact]
    public async Task GameVersionFiltersEverythingAndUnknownVersionIsEmpty()
    {
        using var seed = new AnalyticsSeed();
        for (var i = 0; i < 4; i++) seed.Match(Day(i), 1, 0, version: AnalyticsSeed.V26, ourSr: 1400 + i);
        for (var i = 10; i < 12; i++) seed.Match(Day(i), 0, 1, version: AnalyticsSeed.V27, ourSr: 1600);
        var service = seed.Wrapped();

        var all = await service.GetWrappedAsync(AnalyticsSeed.Club, null, default);
        Assert.Equal(6, all.Totals.Matches);
        Assert.Null(all.GameVersion);
        Assert.Null(all.GameVersionName);

        var v27 = await service.GetWrappedAsync(AnalyticsSeed.Club, 27, default);
        Assert.Equal((2, 0, 2), (v27.Totals.Matches, v27.Totals.Wins, v27.Totals.Losses));
        Assert.Equal((27, "FC27"), (v27.GameVersion, v27.GameVersionName));
        Assert.Equal(new DateOnly(2026, 9, 11), v27.From);
        Assert.Equal(1600, v27.Progression.SkillRating.Start);
        Assert.Null(v27.Streaks.LongestWin);

        var v26 = await service.GetWrappedAsync(AnalyticsSeed.Club, 26, default);
        Assert.Equal(4, v26.Totals.Matches);
        Assert.Equal(4, v26.Streaks.LongestWin!.Length);
        Assert.Equal((1400, 1403), (v26.Progression.SkillRating.Start, v26.Progression.SkillRating.End));

        var unknown = await service.GetWrappedAsync(AnalyticsSeed.Club, 99, default);
        Assert.Equal(0, unknown.Totals.Matches);
        Assert.Equal(99, unknown.GameVersion);
        Assert.Null(unknown.GameVersionName);
    }

    [Fact]
    public async Task EmptyClubReturnsAWellFormedEmptyResult()
    {
        using var seed = new AnalyticsSeed();

        var w = await seed.Wrapped().GetWrappedAsync(AnalyticsSeed.Club, null, default);

        Assert.Equal(AnalyticsSeed.Club, w.ClubId);
        Assert.Equal(0, w.Totals.Matches);
        Assert.Equal(0, w.Totals.WinRatePct);
        Assert.Null(w.From);
        Assert.Null(w.To);
        Assert.Null(w.Streaks.LongestWin);
        Assert.Null(w.BigMoments.BiggestWin);
        Assert.Null(w.BigMoments.HighestScoring);
        Assert.Null(w.Players.TopScorer);
        Assert.Null(w.BestDuo);
        Assert.Null(w.Opponents.MostFaced);
        Assert.Null(w.Rhythm.BusiestWeekday);
        Assert.Empty(w.Rhythm.Monthly);
        Assert.Empty(w.Progression.SrSeries);
        Assert.Empty(w.FunFacts);
    }

    [Fact]
    public async Task RhythmUsesTheLocalTimeZoneAndBestMonthNeedsFiveMatches()
    {
        using var seed = new AnalyticsSeed();
        // Segunda 28/09 em São Paulo (terça em UTC), 22:00-23:20 local.
        foreach (var minute in new[] { 0, 30, 60, 80 })
            seed.Match(new DateTime(2026, 9, 29, 1, 0, 0, DateTimeKind.Utc).AddMinutes(minute), 1, 0);
        // 6 partidas em agosto: 3 vitórias e 3 derrotas.
        for (var i = 0; i < 6; i++) seed.Match(new DateTime(2026, 8, 10 + i, 15, 0, 0, DateTimeKind.Utc), i % 2, (i + 1) % 2);

        var w = await seed.Wrapped().GetWrappedAsync(AnalyticsSeed.Club, null, default);

        Assert.Equal(new[] { "2026-08", "2026-09" }, w.Rhythm.Monthly.Select(m => m.Month));
        Assert.Equal((6, 3, 0, 3), (w.Rhythm.Monthly[0].Matches, w.Rhythm.Monthly[0].Wins, w.Rhythm.Monthly[0].Draws, w.Rhythm.Monthly[0].Losses));
        Assert.Equal(4, w.Rhythm.Monthly[1].Matches);
        // setembro (4 partidas, 100%) não qualifica; agosto (6 jogos, 50%) sim.
        Assert.Equal(("2026-08", 50.0, 6), (w.Rhythm.BestMonth!.Month, w.Rhythm.BestMonth.WinRatePct, w.Rhythm.BestMonth.Matches));
        // As 4 de setembro: 22h/23h locais (1h/2h UTC); agosto: 12h local. Mais frequente = 12h (6 jogos).
        Assert.Equal((12, 6), (w.Rhythm.BusiestHour.Hour, w.Rhythm.BusiestHour.Matches));
        // Dias da semana locais: 10..15/ago/2026 = segunda..sábado (1 cada); segunda 28/09 tem 4 -> segunda com 5.
        Assert.Equal((1, 5), (w.Rhythm.BusiestWeekday!.Weekday, w.Rhythm.BusiestWeekday.Matches));
    }

    [Fact]
    public async Task FunFactsAreBetweenThreeAndSixPortugueseSentences()
    {
        using var seed = LongSrSeed(60);

        var w = await seed.Wrapped().GetWrappedAsync(AnalyticsSeed.Club, null, default);

        Assert.InRange(w.FunFacts.Count, 3, 6);
        Assert.StartsWith("Vocês jogaram o equivalente a", w.FunFacts[0]);
        Assert.Contains($"{w.Totals.Sessions} noites", w.FunFacts[0]);
        Assert.All(w.FunFacts, f => Assert.EndsWith(".", f));
    }
}
