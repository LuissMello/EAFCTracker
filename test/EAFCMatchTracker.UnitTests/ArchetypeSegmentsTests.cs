using EAFCMatchTracker.Application.Dtos;
using EAFCMatchTracker.Application.Services.Analytics;
using EAFCMatchTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EAFCMatchTracker.UnitTests;

/// <summary>PlayerStatisticsDto.Segments: uma linha por (arquétipo, grupo da posição) quando o jogador teve mais de uma combinação.</summary>
public class ArchetypeSegmentsTests
{
    private static readonly DateTime T0 = AnalyticsSeed.Base;

    /// <summary>
    /// 11 = "Zeca": meia arq 7 (x2), meia arq 9 (x2), zagueiro arq 6 (x1) -> 3 combinações. 12: sempre atacante arq 12 -> 1 combinação.
    /// Estatísticas variadas para as somas não baterem por acaso.
    /// </summary>
    private static (AnalyticsSeed Seed, List<MatchPlayerEntity> Rows) Seeded()
    {
        var s = new AnalyticsSeed();
        var plan = new (string Pos, int Arch)[] { ("midfielder", 7), ("midfielder", 7), ("midfielder", 9), ("midfielder", 9), ("defender", 6) };
        for (var i = 0; i < plan.Length; i++)
            s.Match(T0.AddMinutes(i * 40), i % 3, 1, ours: new[]
            {
                new Pl(11, Goals: i % 2, Assists: i % 3, Pre: i % 2, Rating: 6 + i * 0.5, Mom: i == 3, Reds: i == 4 ? 1 : 0, Pos: plan[i].Pos, Arch: plan[i].Arch),
                new Pl(12, Goals: 1, Rating: 7, Pos: "forward", Arch: 12)
            });
        foreach (var mp in s.Db.MatchPlayers.Local.Where(m => m.ClubId == AnalyticsSeed.Club))
        {
            var k = (int)mp.MatchId + (int)mp.PlayerEntityId;
            mp.Shots = (short)(2 + k % 3); mp.Passattempts = (short)(20 + k); mp.Passesmade = (short)(10 + k % 7);
            mp.Tackleattempts = (short)(3 + k % 2); mp.Tacklesmade = (short)(1 + k % 2); mp.Saves = (short)(k % 2);
            mp.Cleansheetsany = (short)(k % 2); mp.SecondsPlayed = 5000; mp.GameTime = 5400;
            mp.Wins = (short)(mp.MatchId % 2); mp.Losses = (short)(1 - mp.MatchId % 2);
        }
        s.Db.SaveChanges();
        var rows = s.Db.MatchPlayers.AsNoTracking().Include(m => m.Player).Include(m => m.Match)
            .Where(m => m.ClubId == AnalyticsSeed.Club).ToList();
        return (s, rows);
    }

    private static void AssertSegmentsSumToTheMainRow(PlayerStatisticsDto main)
    {
        var seg = main.Segments;
        int Sum(Func<PlayerStatisticsDto, int> f) => seg.Sum(f);
        Assert.Equal(main.MatchesPlayed, Sum(s => s.MatchesPlayed));
        Assert.Equal(main.TotalGoals, Sum(s => s.TotalGoals));
        Assert.Equal(main.TotalAssists, Sum(s => s.TotalAssists));
        Assert.Equal(main.TotalPreAssists, Sum(s => s.TotalPreAssists));
        Assert.Equal(main.TotalShots, Sum(s => s.TotalShots));
        Assert.Equal(main.TotalPassesMade, Sum(s => s.TotalPassesMade));
        Assert.Equal(main.TotalPassAttempts, Sum(s => s.TotalPassAttempts));
        Assert.Equal(main.TotalTacklesMade, Sum(s => s.TotalTacklesMade));
        Assert.Equal(main.TotalTackleAttempts, Sum(s => s.TotalTackleAttempts));
        Assert.Equal(main.TotalWins, Sum(s => s.TotalWins));
        Assert.Equal(main.TotalLosses, Sum(s => s.TotalLosses));
        Assert.Equal(main.TotalDraws, Sum(s => s.TotalDraws));
        Assert.Equal(main.TotalCleanSheets, Sum(s => s.TotalCleanSheets));
        Assert.Equal(main.TotalRedCards, Sum(s => s.TotalRedCards));
        Assert.Equal(main.TotalSaves, Sum(s => s.TotalSaves));
        Assert.Equal(main.TotalMom, Sum(s => s.TotalMom));
        Assert.Equal(main.TotalGoalsConceded, Sum(s => s.TotalGoalsConceded));
        Assert.Equal(main.TotalSecondsPlayed, Sum(s => s.TotalSecondsPlayed));
        Assert.Equal(main.TotalGameTime, Sum(s => s.TotalGameTime));
        // médias NÃO são somadas: nota ponderada pelos jogos bate com a da linha principal
        Assert.Equal(main.AvgRating, seg.Sum(s => s.AvgRating * s.MatchesPlayed) / main.MatchesPlayed, 6);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PlayerWithThreeCombinationsGetsSegmentsThatSumToTheMainRow(bool merged)
    {
        var (seed, rows) = Seeded();
        using var _ = seed;
        var all = merged ? StatsAggregator.BuildPerPlayerMergedByGlobalId(rows) : StatsAggregator.BuildPerPlayer(rows);
        var main = all.Single(p => p.PlayerEntityId == 11);

        Assert.Equal(3, main.Segments.Count);
        // mais jogos primeiro (2, 2, 1); empate entre 7 e 9: o mais recente (9)
        Assert.Equal(new[] { (9, "MEIO"), (7, "MEIO"), (6, "DEFESA") }, main.Segments.Select(s => ((int)s.ArchetypeId, s.PositionGroup!)));
        Assert.Equal(new[] { 2, 2, 1 }, main.Segments.Select(s => s.MatchesPlayed));
        Assert.Equal(new[] { "midfielder", "midfielder", "defender" }, main.Segments.Select(s => s.Position!));
        Assert.All(main.Segments, s =>
        {
            Assert.Equal(s.ArchetypeId, s.Archetype!.Id);
            Assert.Single(s.Archetypes);
            Assert.Empty(s.Segments); // sem aninhamento
            Assert.Equal(11L, s.PlayerEntityId);
            Assert.Equal(main.PlayerName, s.PlayerName);
        });
        AssertSegmentsSumToTheMainRow(main);

        // percentuais recalculados por segmento (não herdados)
        var seg = main.Segments[0];
        Assert.Equal(seg.TotalPassesMade * 100.0 / seg.TotalPassAttempts, seg.PassAccuracyPercent, 6);
        Assert.Equal(seg.TotalTacklesMade * 100.0 / seg.TotalTackleAttempts, seg.TackleSuccessPercent, 6);
        Assert.NotEqual(main.PassAccuracyPercent, seg.PassAccuracyPercent);
        Assert.Equal(seg.TotalWins * 100.0 / seg.MatchesPlayed, seg.WinPercent, 6);

        // jogador com uma só combinação: sem segmentos
        Assert.Empty(all.Single(p => p.PlayerEntityId == 12).Segments);
        // a linha principal segue igual (arquétipo principal e uso)
        Assert.Equal(3, main.Archetypes.Count);
    }

    [Fact]
    public void MissingArchetypeIsItsOwnSegmentWithNullArchetype()
    {
        using var seed = new AnalyticsSeed();
        seed.Match(T0, 1, 0, ours: new[] { new Pl(11, Pos: "midfielder", Arch: 8) });
        seed.Match(T0.AddMinutes(40), 1, 0, ours: new[] { new Pl(11, Pos: "midfielder", Arch: 0) });
        var rows = seed.Db.MatchPlayers.AsNoTracking().Include(m => m.Player).Include(m => m.Match).ToList();

        var main = Assert.Single(StatsAggregator.BuildPerPlayer(rows));
        Assert.Equal(2, main.Segments.Count);
        var none = main.Segments.Single(s => s.ArchetypeId == 0);
        Assert.Null(none.Archetype);
        Assert.Empty(none.Archetypes);
        Assert.Equal(1, none.MatchesPlayed);
    }

    [Fact]
    public void SameArchetypeButAnotherPositionGroupAlsoSplits_SingleCombinationDoesNot()
    {
        using var seed = new AnalyticsSeed();
        seed.Match(T0, 1, 0, ours: new[] { new Pl(11, Pos: "midfielder", Arch: 8), new Pl(12, Pos: "midfielder", Arch: 8) });
        seed.Match(T0.AddMinutes(40), 1, 0, ours: new[] { new Pl(11, Pos: "forward", Arch: 8), new Pl(12, Pos: "CAM", Arch: 8) }); // CAM = meio
        var rows = seed.Db.MatchPlayers.AsNoTracking().Include(m => m.Player).Include(m => m.Match).ToList();
        var all = StatsAggregator.BuildPerPlayer(rows);

        Assert.Equal(new[] { "ATAQUE", "MEIO" }, all.Single(p => p.PlayerEntityId == 11).Segments.Select(s => s.PositionGroup!).OrderBy(g => g));
        Assert.Empty(all.Single(p => p.PlayerEntityId == 12).Segments); // "midfielder" e "CAM" são o mesmo grupo
    }

    [Fact]
    public void SingleMatchRowHasNoSegmentsAndSegmentsGetCatalogLabels()
    {
        var (seed, rows) = Seeded();
        using var _ = seed;
        var one = StatsAggregator.BuildPerPlayer(rows.Where(r => r.MatchId == rows.Min(x => x.MatchId)));
        Assert.All(one, p => Assert.Empty(p.Segments));

        // o catálogo troca os rótulos também dentro dos segmentos
        var main = StatsAggregator.BuildPerPlayer(rows).Single(p => p.PlayerEntityId == 11);
        var snap = new ArchetypeCatalogSnapshot(new Dictionary<int, PlayerArchetypeEntity>(), new Dictionary<int, string?>(), true, 1);
        snap.Apply(main);
        Assert.Equal(new[] { "Creator", "Recycler", "Marauder" }, main.Segments.Select(s => s.Archetype!.Label));
    }
}
