using System.Globalization;
using EAFCMatchTracker.Application.Services;
using EAFCMatchTracker.Application.Services.Analytics;
using Xunit;

namespace EAFCMatchTracker.UnitTests;

public class LabServiceTests
{
    private static readonly DateTime T0 = new(2026, 9, 1, 15, 0, 0, DateTimeKind.Utc);
    private static DateTime Day(int i) => T0.AddDays(i); // uma partida por dia = uma sessão por partida

    // ------------------------------------------------------------------ player impact

    /// <summary>Jogador 11 (A) em todas as 10 partidas; 12 (B) só nas 6 primeiras (vitórias 2-0); nas 4 últimas só A (derrotas 0-1).</summary>
    private static AnalyticsSeed WithWithoutSeed()
    {
        var s = new AnalyticsSeed();
        for (var i = 0; i < 10; i++)
            s.Match(Day(i), i < 6 ? 2 : 0, i < 6 ? 0 : 1,
                ours: i < 6 ? new[] { new Pl(11), new Pl(12) } : new[] { new Pl(11) });
        return s;
    }

    [Fact]
    public async Task PlayerImpactComputesWithAndWithoutExactly()
    {
        using var seed = WithWithoutSeed();

        var r = await seed.Lab().GetPlayerImpactAsync(AnalyticsSeed.Club, null, null, null, 3, default);

        Assert.Equal(10, r.TotalMatches);
        Assert.Equal((10, 6, 0, 4, 60.0, 1.8, 1.2, 0.4),
            (r.Baseline.Matches, r.Baseline.Wins, r.Baseline.Draws, r.Baseline.Losses, r.Baseline.WinRatePct,
             r.Baseline.PointsPerMatch, r.Baseline.GoalsForPerMatch, r.Baseline.GoalsAgainstPerMatch));

        var b = r.Players.Single(p => p.PlayerEntityId == 12);
        Assert.Equal((6, 6, 100.0, 3.0, 2.0, 0.0), (b.With.Matches, b.With.Wins, b.With.WinRatePct, b.With.PointsPerMatch, b.With.GoalsForPerMatch, b.With.GoalsAgainstPerMatch));
        Assert.Equal((4, 0, 0.0, 0.0, 1.0), (b.Without!.Matches, b.Without.Wins, b.Without.WinRatePct, b.Without.PointsPerMatch, b.Without.GoalsAgainstPerMatch));
        Assert.Equal((100.0, 3.0, 3.0), (b.Delta!.WinRatePct, b.Delta.PointsPerMatch, b.Delta.GoalDiffPerMatch));
        Assert.Equal("low", b.Reliability); // min(6, 4) = 4 < 5
        Assert.Equal("Poucos jogos sem este jogador (4); leia com cautela.", b.Note);

        // Jogador presente em todas as partidas: sem grupo "sem", sem delta, ordenado por último.
        var a = r.Players.Single(p => p.PlayerEntityId == 11);
        Assert.Null(a.Without);
        Assert.Null(a.Delta);
        Assert.Equal("low", a.Reliability);
        Assert.NotNull(a.Note);
        Assert.Equal(new long[] { 12, 11 }, r.Players.Select(p => p.PlayerEntityId));
    }

    [Fact]
    public async Task PlayerImpactHonoursMinMatchesAndClampsIt()
    {
        using var seed = WithWithoutSeed();
        var lab = seed.Lab();

        var strict = await lab.GetPlayerImpactAsync(AnalyticsSeed.Club, null, null, null, 7, default);
        Assert.Equal(new long[] { 11 }, strict.Players.Select(p => p.PlayerEntityId)); // 12 só jogou 6

        var zero = await lab.GetPlayerImpactAsync(AnalyticsSeed.Club, null, null, null, 0, default); // 0 vira 1
        Assert.Equal(2, zero.Players.Count);

        var huge = await lab.GetPlayerImpactAsync(AnalyticsSeed.Club, null, null, null, 500, default); // 500 vira 30
        Assert.Empty(huge.Players);
    }

    [Theory]
    [InlineData(0, "low")]
    [InlineData(4, "low")]
    [InlineData(5, "medium")]
    [InlineData(11, "medium")]
    [InlineData(12, "high")]
    [InlineData(40, "high")]
    public void ReliabilityThresholdsUseTheSmallerSample(int smallest, string expected) =>
        Assert.Equal(expected, StatsUtil.Reliability(smallest));

    [Fact]
    public async Task ReliabilityFollowsTheSmallerOfWithAndWithout()
    {
        using var seed = new AnalyticsSeed();
        // 12 com + 8 sem para o jogador 12 (menor lado = 8 -> medium); 15 com + 15 sem para o 13 -> high.
        for (var i = 0; i < 30; i++)
        {
            var list = new List<Pl> { new(11) };
            if (i < 12) list.Add(new Pl(12));
            if (i % 2 == 0) list.Add(new Pl(13));
            seed.Match(Day(i), i % 3 == 0 ? 0 : 1, 0, ours: list.ToArray());
        }

        var r = await seed.Lab().GetPlayerImpactAsync(AnalyticsSeed.Club, null, null, null, 3, default);

        Assert.Equal("high", r.Players.Single(p => p.PlayerEntityId == 13).Reliability);
        Assert.Null(r.Players.Single(p => p.PlayerEntityId == 13).Note);
        var p12 = r.Players.Single(p => p.PlayerEntityId == 12);
        Assert.Equal((12, 18), (p12.With.Matches, p12.Without!.Matches));
        Assert.Equal("high", p12.Reliability); // min(12, 18) = 12
    }

    [Fact]
    public async Task PlayerImpactRespectsDateRangeInTheClubTimeZoneAndVersion()
    {
        using var seed = new AnalyticsSeed();
        // 29/09 01:00Z = 28/09 22:00 em São Paulo: cai em 28/09, não em 29/09.
        seed.Match(new DateTime(2026, 9, 29, 1, 0, 0, DateTimeKind.Utc), 1, 0, ours: new[] { new Pl(11) });
        seed.Match(new DateTime(2026, 9, 29, 15, 0, 0, DateTimeKind.Utc), 1, 0, ours: new[] { new Pl(11) });
        seed.Match(new DateTime(2025, 9, 29, 15, 0, 0, DateTimeKind.Utc), 1, 0, version: AnalyticsSeed.V26, ours: new[] { new Pl(11) });
        var lab = seed.Lab();

        var only28 = await lab.GetPlayerImpactAsync(AnalyticsSeed.Club, new DateOnly(2026, 9, 28), new DateOnly(2026, 9, 28), null, 1, default);
        Assert.Equal(1, only28.TotalMatches);
        var only29 = await lab.GetPlayerImpactAsync(AnalyticsSeed.Club, new DateOnly(2026, 9, 29), new DateOnly(2026, 9, 29), null, 1, default);
        Assert.Equal(1, only29.TotalMatches);
        Assert.Equal(3, (await lab.GetPlayerImpactAsync(AnalyticsSeed.Club, null, null, null, 1, default)).TotalMatches);
        Assert.Equal(1, (await lab.GetPlayerImpactAsync(AnalyticsSeed.Club, null, null, 26, 1, default)).TotalMatches);
        Assert.Equal(0, (await lab.GetPlayerImpactAsync(AnalyticsSeed.Club, null, null, 99, 1, default)).TotalMatches);
    }

    // ------------------------------------------------------------------ context

    [Fact]
    public async Task ContextBucketsByLocalWeekdayHourPlayersAndOpponentStrength()
    {
        using var seed = new AnalyticsSeed();
        // Todas na segunda 28/09 em São Paulo (terça 29/09 em UTC): 22:00, 22:30, 23:00, 23:20.
        seed.Match(new DateTime(2026, 9, 29, 1, 0, 0, DateTimeKind.Utc), 2, 0, ourSr: 1500, oppSr: 1560, oppPlayers: 3, ours: new[] { new Pl(11), new Pl(12) });
        seed.Match(new DateTime(2026, 9, 29, 1, 30, 0, DateTimeKind.Utc), 0, 1, ourSr: 1500, oppSr: 1440, oppPlayers: 3, ours: new[] { new Pl(11), new Pl(12), new Pl(13) });
        seed.Match(new DateTime(2026, 9, 29, 2, 0, 0, DateTimeKind.Utc), 1, 1, ourSr: 1500, oppSr: 1530, oppPlayers: 4, ours: new[] { new Pl(11), new Pl(12) });
        seed.Match(new DateTime(2026, 9, 29, 2, 20, 0, DateTimeKind.Utc), 3, 0, ourSr: 1500, oppPlayers: 4, ours: new[] { new Pl(11), new Pl(12) });

        var c = await seed.Lab().GetContextAsync(AnalyticsSeed.Club, null, null, null, default);

        Assert.Equal("America/Sao_Paulo", c.TimeZoneId);
        Assert.Equal(4, c.Baseline.Matches);

        var monday = Assert.Single(c.ByWeekday);
        Assert.Equal(1, monday.Weekday); // segunda (UTC seria terça = 2)
        Assert.Equal(4, monday.Matches);
        Assert.Equal(new[] { 22, 23 }, c.ByHour.Select(h => h.Hour));
        Assert.Equal(new[] { 2, 2 }, c.ByHour.Select(h => h.Matches));
        Assert.DoesNotContain(c.ByHour, h => h.Hour == 1 || h.Hour == 2); // horas UTC não aparecem

        Assert.Equal(new[] { 2, 3 }, c.ByOurPlayers.Select(b => b.Players));
        Assert.Equal(new[] { 3, 1 }, c.ByOurPlayers.Select(b => b.Matches));
        Assert.Equal(new[] { 3, 4 }, c.ByOpponentPlayers.Select(b => b.Players));
        Assert.Equal(new[] { 2, 2 }, c.ByOpponentPlayers.Select(b => b.Matches));

        // Faixas por diferença de SR (adversário − nosso): +60 mais forte, −60 mais fraco, +30 parecido; sem SR do rival é omitida.
        Assert.Equal(new[] { LabService.BandWeaker, LabService.BandSimilar, LabService.BandStronger }, c.ByOpponentStrength.Select(b => b.Band));
        Assert.All(c.ByOpponentStrength, b => Assert.Equal(1, b.Matches));
        Assert.Equal((-60, -60), (c.ByOpponentStrength[0].SrGapMin, c.ByOpponentStrength[0].SrGapMax));
        Assert.Equal((30, 30), (c.ByOpponentStrength[1].SrGapMin, c.ByOpponentStrength[1].SrGapMax));
        Assert.Equal(60, c.ByOpponentStrength[2].SrGapMin);
    }

    [Theory]
    [InlineData(-200, "mais fraco")]
    [InlineData(-60, "mais fraco")]
    [InlineData(-59, "parecido")]
    [InlineData(0, "parecido")]
    [InlineData(59, "parecido")]
    [InlineData(60, "mais forte")]
    [InlineData(300, "mais forte")]
    public void OpponentStrengthBandBoundaries(int gap, string band) => Assert.Equal(band, LabService.BandOf(gap));

    [Fact]
    public async Task ContextOmitsStrengthWhenThereAreNoSkillRatings()
    {
        using var seed = new AnalyticsSeed();
        seed.Match(Day(0), 1, 0);
        var c = await seed.Lab().GetContextAsync(AnalyticsSeed.Club, null, null, null, default);
        Assert.Empty(c.ByOpponentStrength);
        Assert.Empty(c.ByOurPlayers); // sem jogadores registrados nas partidas
        Assert.Single(c.ByWeekday);
    }

    [Fact]
    public async Task SessionPositionIsCappedAtEightAndUsesTheFullSession()
    {
        using var seed = new AnalyticsSeed();
        // Uma única sessão de 10 partidas (10 min entre elas): posições 1..8, sendo 8 = "8 ou mais" (3 partidas).
        for (var i = 0; i < 10; i++) seed.Match(T0.AddMinutes(i * 10), i < 8 ? 1 : 0, 0);
        // Outra sessão (dias depois) com 2 partidas.
        seed.Match(T0.AddDays(3), 1, 0);
        seed.Match(T0.AddDays(3).AddMinutes(20), 0, 1);

        var c = await seed.Lab().GetContextAsync(AnalyticsSeed.Club, null, null, null, default);

        Assert.Equal(Enumerable.Range(1, 8), c.BySessionPosition.Select(p => p.Position));
        var byPosition = c.BySessionPosition.ToDictionary(p => p.Position);
        Assert.Equal(2, byPosition[1].Matches);       // 1ª de cada sessão
        Assert.Equal(2, byPosition[2].Matches);
        Assert.Equal(1, byPosition[3].Matches);
        Assert.Equal(3, byPosition[8].Matches);       // posições 8, 9 e 10
        Assert.Equal(12, c.BySessionPosition.Sum(p => p.Matches));
    }

    [Fact]
    public async Task SessionPositionIsNotRestartedByAPeriodFilter()
    {
        using var seed = new AnalyticsSeed();
        // Sessão de 3 partidas; o filtro de edição isola só a 3ª, que continua sendo a posição 3 da sessão.
        seed.Match(T0, 1, 0, version: AnalyticsSeed.V26);
        seed.Match(T0.AddMinutes(10), 1, 0, version: AnalyticsSeed.V26);
        seed.Match(T0.AddMinutes(20), 1, 0, version: AnalyticsSeed.V27);

        var c = await seed.Lab().GetContextAsync(AnalyticsSeed.Club, null, null, 27, default);

        Assert.Equal(3, Assert.Single(c.BySessionPosition).Position);
    }

    // ------------------------------------------------------------------ duos

    [Fact]
    public async Task DuosRankByPointsGainedTogetherVersusApart()
    {
        using var seed = new AnalyticsSeed();
        // 11 em todas; 12 nas 5 primeiras (vitórias); 13 nas 5 últimas (derrotas).
        for (var i = 0; i < 10; i++)
            seed.Match(Day(i), i < 5 ? 1 : 0, i < 5 ? 0 : 1,
                ours: i < 5 ? new[] { new Pl(11), new Pl(12) } : new[] { new Pl(11), new Pl(13) });

        var duos = await seed.Lab().GetDuosAsync(AnalyticsSeed.Club, null, null, null, 5, default);

        var best = Assert.Single(duos.Best);
        Assert.Equal((11L, 12L), (best.APlayerEntityId, best.BPlayerEntityId));
        Assert.Equal(("Jogador 11", "Jogador 12"), (best.AName, best.BName));
        Assert.Equal((5, 5, 3.0), (best.Together.Matches, best.Together.Wins, best.Together.PointsPerMatch));
        Assert.Equal((5, 0, 0.0), (best.Apart!.Matches, best.Apart.Wins, best.Apart.PointsPerMatch));
        Assert.Equal((100.0, 3.0), (best.Delta!.WinRatePct, best.Delta.PointsPerMatch));
        Assert.Equal("medium", best.Reliability);

        var worst = Assert.Single(duos.Worst);
        Assert.Equal((11L, 13L), (worst.APlayerEntityId, worst.BPlayerEntityId));
        Assert.Equal(-3.0, worst.Delta!.PointsPerMatch);
        // 12+13 nunca jogaram juntos (0 < minMatches): não aparecem.
    }

    [Fact]
    public async Task DuosNeedMinMatchesTogetherAndAComparison()
    {
        using var seed = new AnalyticsSeed();
        // 11 e 12 jogam sempre juntos: sem partidas "separados" não há comparação.
        for (var i = 0; i < 6; i++) seed.Match(Day(i), 1, 0, ours: new[] { new Pl(11), new Pl(12) });

        var duos = await seed.Lab().GetDuosAsync(AnalyticsSeed.Club, null, null, null, 5, default);
        Assert.Empty(duos.Best);
        Assert.Empty(duos.Worst);
    }

    [Fact]
    public void DuoPairEnumerationIsCappedToTheMostFrequentPlayers()
    {
        var played = new Dictionary<long, HashSet<long>>();
        for (long p = 1; p <= 40; p++)
            played[p] = Enumerable.Range(1, p <= 30 ? 10 : 5).Select(x => (long)x).ToHashSet();
        played[99] = Enumerable.Range(1, 2).Select(x => (long)x).ToHashSet(); // abaixo do mínimo

        var pool = LabService.DuoPool(played, 5);

        Assert.Equal(LabService.MaxDuoPlayers, pool.Count);
        Assert.Equal(Enumerable.Range(1, LabService.MaxDuoPlayers).Select(x => (long)x), pool.OrderBy(x => x));
        Assert.DoesNotContain(99L, pool);
    }

    // ------------------------------------------------------------------ parsing

    [Theory]
    [InlineData("1450", 1450)]
    [InlineData(" 1450 ", 1450)]
    [InlineData("1,450", 1450)]
    [InlineData("1450.0", 1450)]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("abc", null)]
    [InlineData("0", null)]
    [InlineData("-20", null)]
    public void SkillRatingParsingIsTolerant(string? raw, int? expected) => Assert.Equal(expected, StatsUtil.ParseSr(raw));

    [Fact]
    public void SkillRatingParsingIgnoresTheCurrentCulture()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pt-BR"); // "." seria separador de milhar
            Assert.Equal(1451, StatsUtil.ParseSr("1450.5"));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }
}
