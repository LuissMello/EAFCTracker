using EAFCMatchTracker.Application.Services.Analytics;
using Xunit;

namespace EAFCMatchTracker.UnitTests;

public class GameNightServiceTests
{
    private static DateTime Utc(int month, int day, int hour, int minute = 0) =>
        new(2026, month, day, hour, minute, 0, DateTimeKind.Utc);

    /// <summary>
    /// noite 0: 20/09 19:00 e 20:00 (SP) · noite 1: segunda 28/09 22:00 → terça 00:50 (cruza a meia-noite) · noite 2: 01/10.
    /// Ids das partidas: 1,2 | 3,4,5 | 6.
    /// </summary>
    private static (AnalyticsSeed Seed, long[] Ids) Seeded()
    {
        var s = new AnalyticsSeed();
        s.Player(13, "Jogador 13");
        var ids = new long[6];
        ids[0] = s.Match(Utc(9, 20, 22), 1, 0, ours: new[] { new Pl(11, Goals: 1) }, ourSr: 1500, division: 5);
        ids[1] = s.Match(Utc(9, 20, 23), 2, 1, ours: new[] { new Pl(11, Goals: 2) }, ourSr: 1510);
        // Noite 1 (segunda 22:00 SP = terça 01:00 UTC)
        ids[2] = s.Match(Utc(9, 29, 1), 4, 0, oppName: "Rival FC", oppPlayers: 4, ourSr: 1520, oppSr: 1560,
            ours: new[] { new Pl(11, Goals: 3, Rating: 8.5, Mom: true), new Pl(12, Assists: 2, Rating: 7.0) });
        ids[3] = s.Match(Utc(9, 29, 2, 30), 0, 2, oppId: 201, oppName: "Outro FC", oppPlayers: 5, ourSr: 1500,
            ours: new[] { new Pl(11, Rating: 6.0, Reds: 1), new Pl(12, Rating: 6.5) });
        ids[4] = s.Match(Utc(9, 29, 3, 50), 1, 1, oppName: "Rival FC", oppPlayers: 4, ourSr: 1505, division: 4,
            ours: new[] { new Pl(11, Goals: 1, Rating: 7.5), new Pl(12, Rating: 7.0, Mom: true) });
        ids[5] = s.Match(Utc(10, 1, 22), 3, 1, ourSr: 1530, ours: new[] { new Pl(11, Goals: 2) });

        s.Goal(ids[2], 11, 12, 13);
        s.Goal(ids[2], 11, 12);
        s.Goal(ids[2], 11);
        s.Goal(ids[2], 12, 11);
        return (s, ids);
    }

    [Fact]
    public async Task ListKeepsAMidnightCrossingNightAsOneWithItsLocalStartDate()
    {
        var (seed, ids) = Seeded();
        using var _ = seed;

        var list = await seed.Nights().GetNightsAsync(AnalyticsSeed.Club, null, default);

        Assert.Equal("America/Sao_Paulo", list.TimeZoneId);
        Assert.Equal(3, list.Nights.Count);
        Assert.Equal(new[] { ids[0], ids[2], ids[5] }, list.Nights.Select(n => n.SessionId));
        var overnight = list.Nights[1];
        Assert.Equal(new DateOnly(2026, 9, 28), overnight.Date); // dia local do início, não terça
        Assert.Equal(3, overnight.Matches);
        Assert.Equal(Utc(9, 29, 1), overnight.StartedAtUtc);
        Assert.Equal(Utc(9, 29, 3, 50), overnight.EndedAtUtc);
        Assert.Equal((1, 1, 1), (overnight.Wins, overnight.Draws, overnight.Losses));
        Assert.Equal((5, 3), (overnight.GoalsFor, overnight.GoalsAgainst));
        Assert.True(list.Nights.Zip(list.Nights.Skip(1), (a, b) => a.StartedAtUtc < b.StartedAtUtc).All(x => x));
    }

    [Fact]
    public async Task DetailHasRecordNavigationSkillRatingAndDivision()
    {
        var (seed, ids) = Seeded();
        using var _ = seed;

        var d = (await seed.Nights().GetNightAsync(AnalyticsSeed.Club, ids[2], null, default))!;

        Assert.Equal("Meu Clube", d.ClubName);
        Assert.Equal(new DateOnly(2026, 9, 28), d.Date);
        Assert.Equal(170, d.DurationMinutes);
        Assert.Equal((2, 3), (d.Index, d.Total));
        Assert.Equal(ids[0], d.PrevSessionId);
        Assert.Equal(ids[5], d.NextSessionId);

        Assert.Equal(3, d.Record.Matches);
        Assert.Equal(33.33, d.Record.WinRatePct);
        Assert.Equal((5, 3, 2, 1), (d.Record.GoalsFor, d.Record.GoalsAgainst, d.Record.GoalDiff, d.Record.CleanSheets));

        // SR "antes" = snapshot da última partida anterior (mesma edição); "depois" = da última partida da noite.
        Assert.Equal(1510, d.SkillRating.Start);
        Assert.Equal(1505, d.SkillRating.End);
        Assert.Equal(-5, d.SkillRating.Delta);
        Assert.Equal(5, d.Division.Start); // vem da partida 1 (a 2 não tem divisão)
        Assert.Equal(4, d.Division.End);
    }

    [Fact]
    public async Task FirstNightFallsBackToTheSnapshotAfterItsFirstMatch()
    {
        var (seed, ids) = Seeded();
        using var _ = seed;

        var d = (await seed.Nights().GetNightAsync(AnalyticsSeed.Club, ids[0], null, default))!;

        Assert.Null(d.PrevSessionId);
        Assert.Equal(ids[2], d.NextSessionId);
        Assert.Equal(1500, d.SkillRating.Start);
        Assert.Equal(1510, d.SkillRating.End);
        Assert.Equal(10, d.SkillRating.Delta);
    }

    [Fact]
    public async Task DetailHighlightsPlayersOpponentsAndGoalChains()
    {
        var (seed, ids) = Seeded();
        using var _ = seed;

        var d = (await seed.Nights().GetNightAsync(AnalyticsSeed.Club, ids[2], null, default))!;
        var h = d.Highlights;

        Assert.Equal((ids[2], "Rival FC", 4, 0), (h.BiggestWin!.MatchId, h.BiggestWin.OpponentName, h.BiggestWin.GoalsFor, h.BiggestWin.GoalsAgainst));
        Assert.Equal((ids[3], "Outro FC"), (h.WorstLoss!.MatchId, h.WorstLoss.OpponentName));
        Assert.Equal(("Jogador 11", 4), (h.TopScorer!.Name, h.TopScorer.Goals));
        Assert.Equal(("Jogador 12", 2), (h.TopAssister!.Name, h.TopAssister.Assists));
        Assert.Equal("Jogador 11", h.BestRated!.Name); // (8,5 + 6,0 + 7,5) / 3
        Assert.Equal(7.33, h.BestRated.AvgRating);
        Assert.Equal(3, h.BestRated.Matches);
        Assert.Equal(("Jogador 11", 1), (h.ManOfTheMatch!.Name, h.ManOfTheMatch.Count)); // empate 1x1: desempata por gols
        var hatTrick = Assert.Single(h.HatTricks);
        Assert.Equal((11L, ids[2]), (hatTrick.PlayerEntityId, hatTrick.MatchId));
        Assert.Equal(1, h.RedCards);

        Assert.Equal(new long[] { 11, 12 }, d.Players.Select(p => p.PlayerEntityId));
        var p11 = d.Players[0];
        Assert.Equal((3, 4, 0, 1, 1), (p11.Matches, p11.Goals, p11.Assists, p11.Motm, p11.RedCards));
        Assert.Equal("forward", p11.Position);

        Assert.Equal(new[] { "Rival FC", "Outro FC" }, d.Opponents.Select(o => o.Name));
        var rival = d.Opponents[0];
        Assert.Equal((2, 1, 1, 0), (rival.Matches, rival.Wins, rival.Draws, rival.Losses));
        Assert.Equal("45", rival.CrestAssetId);
        Assert.Equal("777", rival.CustomCrestAssetId);

        Assert.Equal(ids.Skip(2).Take(3), d.Matches.Select(m => m.MatchId));
        var first = d.Matches[0];
        Assert.Equal(("W", 2, 4, 1520), (first.Result, first.OurPlayersCount, first.OpponentPlayersCount, first.SkillRatingAfter));
        Assert.Equal(4, first.Goals.Count);
        Assert.Equal(("Jogador 11", "Jogador 12", "Jogador 13"), (first.Goals[0].ScorerName, first.Goals[0].AssistName, first.Goals[0].PreAssistName));
        Assert.Null(first.Goals[2].AssistName);
        Assert.Empty(d.Matches[1].Goals);
    }

    [Fact]
    public async Task UnknownOrForeignSessionIsNotFound()
    {
        var (seed, ids) = Seeded();
        using var _ = seed;
        seed.Match(Utc(9, 29, 1), 1, 0, id: 900, clubId: 300, oppId: 301);
        var service = seed.Nights();

        Assert.Null(await service.GetNightAsync(AnalyticsSeed.Club, 900, null, default)); // sessão de outro clube
        Assert.Null(await service.GetNightAsync(AnalyticsSeed.Club, ids[3], null, default)); // partida que não abre a noite
        Assert.Null(await service.GetNightAsync(AnalyticsSeed.Club, 123456, null, default));
        Assert.NotNull(await service.GetNightAsync(300, 900, null, default));
    }

    [Fact]
    public async Task GameVersionFiltersNightsAndUnknownVersionIsEmpty()
    {
        var (seed, ids) = Seeded();
        using var _ = seed;
        var old = seed.Match(Utc(8, 1, 22), 2, 2, version: AnalyticsSeed.V26, ourSr: 1400);
        var service = seed.Nights();

        Assert.Equal(4, (await service.GetNightsAsync(AnalyticsSeed.Club, null, default)).Nights.Count);
        Assert.Equal(3, (await service.GetNightsAsync(AnalyticsSeed.Club, 27, default)).Nights.Count);
        Assert.Equal(new[] { old }, (await service.GetNightsAsync(AnalyticsSeed.Club, 26, default)).Nights.Select(n => n.SessionId));
        Assert.Empty((await service.GetNightsAsync(AnalyticsSeed.Club, 99, default)).Nights);

        // O SR "antes" nunca vem de outra edição: a partida da FC26 não conta para a primeira noite da FC27.
        var firstOf27 = (await service.GetNightAsync(AnalyticsSeed.Club, ids[0], 27, default))!;
        Assert.Equal(1500, firstOf27.SkillRating.Start);
        Assert.Equal(1, firstOf27.Index);
        Assert.Equal(3, firstOf27.Total);
    }

    [Fact]
    public async Task ClubWithoutMatchesReturnsAnEmptyList()
    {
        using var seed = new AnalyticsSeed();
        var list = await seed.Nights().GetNightsAsync(AnalyticsSeed.Club, null, default);
        Assert.Empty(list.Nights);
        Assert.Equal("America/Sao_Paulo", list.TimeZoneId);
    }
}
