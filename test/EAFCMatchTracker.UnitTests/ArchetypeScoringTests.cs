using EAFCMatchTracker.Api.Controllers;
using EAFCMatchTracker.Application.Dtos;
using EAFCMatchTracker.Application.Services.Analytics;
using Microsoft.AspNetCore.Mvc;
using Xunit;
using Xunit.Abstractions;

namespace EAFCMatchTracker.UnitTests;

/// <summary>Pesos do overall por arquétipo, filtro/visão por arquétipo das Cartas e comparação "como arquétipo".</summary>
public class ArchetypeScoringTests
{
    private readonly ITestOutputHelper _out;
    public ArchetypeScoringTests(ITestOutputHelper output) => _out = output;

    private static readonly DateTime T0 = AnalyticsSeed.Base;

    // ------------------------------------------------------------------ tabela de pesos

    [Fact]
    public void EveryArchetypeWeightRowSumsToOneAndCoversTheThirteenKnownIds()
    {
        Assert.Equal(ArchetypeDefaults.Known.Keys.OrderBy(i => i), CardScoring.ArchetypeWeights.Keys.OrderBy(i => i));
        foreach (var (id, w) in CardScoring.ArchetypeWeights)
        {
            Assert.True(Math.Abs(w.Ata + w.Cri + w.Pas + w.Def + w.Imp + w.Reg + w.Gol - 1.0) < 1e-9, $"arquétipo {id} não soma 1");
            Assert.All(new[] { w.Ata, w.Cri, w.Pas, w.Def, w.Imp, w.Reg, w.Gol }, x => Assert.InRange(x, 0, 1));
            // pesos de goleiro (GOL > 0) só nos dois arquétipos de goleiro; o resto nunca pesa GOL
            Assert.Equal(ArchetypeDefaults.Known[id].Group == CardScoring.GroupKeeper, w.Gol > 0);
        }
        Assert.Equal((.45, .05, .05, 0, .30, .15, 0), CardScoring.ArchetypeWeights[12]);
        Assert.Equal((0, 0, .05, 0, .25, .15, .55), CardScoring.ArchetypeWeights[1]);
    }

    private static readonly CardAxes Sample = new(40, 60, 80, 20, 70, 50, null);

    [Fact]
    public void ArchetypeOverallDiffersFromPositionOverallForAKnownSample()
    {
        // com o bônus de +0,10 no peso de impacto (v3): posição 58,97; Maestro 59,85; Creator 64,26
        Assert.Equal(59, CardScoring.Overall("MEIO", Sample));
        Assert.Equal(60, CardScoring.Overall("MEIO", Sample, 8));
        Assert.Equal(64, CardScoring.Overall("MEIO", Sample, 9));
        Assert.Equal(CardScoring.Overall("MEIO", Sample), CardScoring.Overall("MEIO", Sample, null));
    }

    [Theory]
    [InlineData(30)]   // fora da tabela
    [InlineData(0)]    // sem dado
    [InlineData(-1)]
    public void UnknownIdsKeepThePositionGroupWeights(int id)
    {
        Assert.Equal(CardScoring.Overall("MEIO", Sample), CardScoring.Overall("MEIO", Sample, id));
        Assert.Equal(CardScoring.Weights("ATAQUE"), CardScoring.Weights("ATAQUE", id));
        Assert.False(CardScoring.UsesArchetypeWeights(id, false));
    }

    [Fact]
    public void KeeperArchetypesOnlyApplyToKeeperCardsAndViceVersa()
    {
        var keeper = new CardAxes(null, 60, 0, 0, 70, 50, 80);
        Assert.True(CardScoring.UsesArchetypeWeights(1, true));
        Assert.False(CardScoring.UsesArchetypeWeights(1, false)); // Shot Stopper numa carta de linha: pesos da posição
        Assert.False(CardScoring.UsesArchetypeWeights(12, true)); // Finisher numa carta de goleiro: idem
        Assert.Equal(CardScoring.Overall("GOLEIRO", keeper), CardScoring.Overall("GOLEIRO", keeper, 12));
    }

    /// <summary>Overall por posição x por arquétipo dos 4 perfis de referência do clube (os mesmos de CardScoringTests).</summary>
    [Fact]
    public void ReferenceProfilesBeforeAndAfter()
    {
        (string Name, string Group, int Arch, int M, double Gpm, double Shot, double Pass, double Ppm, double Apm, double Pre, double Tk, double Tkm,
            double Rating, double Motm, double Att, double Sd)[] profiles =
        {
            ("L. Mello", "ATAQUE", 12, 18, 0.61, 52.4, 69.5, 25, 0.39, 0.15, 21.6, 1.5, 7.9, 3 / 18.0, 18 / 25.0, 0.7),
            ("Luska", "ATAQUE", 13, 14, 0.64, 69.2, 75.4, 28, 0.36, 0.15, 82.4, 2.0, 8.3, 3 / 14.0, 14 / 25.0, 0.6),
            ("Zaga", "MEIO", 8, 13, 0.38, 33.3, 67.6, 30, 0.62, 0.2, 31.6, 3.0, 7.4, 0, 13 / 25.0, 0.8),
            ("Pedro", "MEIO", 9, 2, 0.5, 50, 72, 28, 0.5, 0.1, 40, 3.0, 6.95, 0, 2 / 25.0, 0.65),
        };
        _out.WriteLine("Perfil     | arquétipo     | posição | arquétipo | delta");
        foreach (var p in profiles)
        {
            var axes = new CardAxes(
                CardScoring.Score(CardScoring.RawAta(p.Gpm, p.Shot), p.M), CardScoring.Score(CardScoring.RawPas(p.Pass, p.Ppm), p.M),
                CardScoring.Score(CardScoring.RawCri(p.Apm, p.Pre), p.M), CardScoring.Score(CardScoring.RawDef(p.Tk, p.Tkm), p.M),
                CardScoring.Score(CardScoring.RawImp(p.Rating, p.Motm), p.M), CardScoring.Score(CardScoring.RawReg(p.Sd, p.M), p.M), null);
            var byPos = CardScoring.Overall(p.Group, axes);
            var byArch = CardScoring.Overall(p.Group, axes, p.Arch);
            _out.WriteLine($"{p.Name,-10} | {ArchetypeDefaults.Known[p.Arch].Name,-13} | {byPos,7} | {byArch,9} | {byArch - byPos,+5}");
            Assert.InRange(byArch, 1, 99);
            Assert.InRange(Math.Abs(byArch - byPos), 0, 8); // mesma base de eixos: a ponderação mexe pouco
        }
        // o Maestro (passe/criação) valoriza o meia criativo; o Finisher (ataque/impacto) o centroavante
        Assert.True(CardScoring.Overall("ATAQUE", new CardAxes(90, 50, 30, 10, 80, 60, null), 12) > CardScoring.Overall("ATAQUE", new CardAxes(90, 50, 30, 10, 80, 60, null)));
    }

    // ------------------------------------------------------------------ serviço

    /// <summary>11 (meia): arq 8 x4, 9 x2, 0 x1, 30 x1. 12 (atacante): arq 12 x6, 11 x2. Uma partida por dia, 8 no total.</summary>
    private static AnalyticsSeed Seeded()
    {
        var s = new AnalyticsSeed();
        var a11 = new[] { 8, 8, 8, 8, 9, 9, 0, 30 };
        var a12 = new[] { 12, 12, 12, 12, 12, 12, 11, 11 };
        for (var i = 0; i < 8; i++)
            s.Match(T0.AddDays(i), 2, 1, ours: new[]
            {
                new Pl(11, Goals: i % 2, Assists: 1, Rating: 6.5 + 0.2 * i, Pos: "midfielder", Arch: a11[i], Ovr: 80),
                new Pl(12, Goals: 1, Rating: 7 + 0.1 * i, Pos: "forward", Arch: a12[i])
            });
        return s;
    }

    private static Task<PlayerCardsDto> Cards(AnalyticsSeed seed, int min = 1, int? arch = null, string? pos = null, string view = "player") =>
        seed.Cards().GetCardsAsync(AnalyticsSeed.Club, null, null, null, min, arch, pos, view, default);

    [Fact]
    public async Task DefaultViewMixesArchetypesSoItKeepsThePositionWeights()
    {
        using var seed = Seeded();
        var r = await Cards(seed);
        Assert.Equal("player", r.View);
        Assert.All(r.Cards, c =>
        {
            Assert.Equal("position", c.Scoring);
            Assert.Equal(c.Overall, c.OverallByPosition);
            Assert.Null(c.SegmentKey);
        });
        Assert.Equal(2, r.Cards.Count);
    }

    [Fact]
    public async Task ArchetypeFilterScoresWithTheArchetypeWeights()
    {
        using var seed = Seeded();
        var r = await Cards(seed, arch: 8);
        var c = Assert.Single(r.Cards);
        Assert.Equal(("archetype", 4), (c.Scoring, c.Matches));

        // recompõe o overall com os eixos já arredondados do cartão: pesos do Maestro x pesos do MEIO (±1 pelo arredondamento dos eixos)
        var axes = new CardAxes(c.Axes.Ata, c.Axes.Pas, c.Axes.Cri, c.Axes.Def, c.Axes.Imp, c.Axes.Reg, null);
        Assert.InRange(c.Overall - CardScoring.Overall("MEIO", axes, 8), -1, 1);
        Assert.InRange(c.OverallByPosition - CardScoring.Overall("MEIO", axes), -1, 1);

        // arquétipo fora da tabela: filtro funciona, mas o overall mantém os pesos da posição
        var thirty = Assert.Single((await Cards(seed, arch: 30)).Cards);
        Assert.Equal(("position", thirty.Overall), (thirty.Scoring, thirty.OverallByPosition));
    }

    [Fact]
    public async Task ArchetypeViewBuildsOneCardPerPlayerAndArchetype()
    {
        using var seed = Seeded();
        var r = await Cards(seed, view: "archetype");

        Assert.Equal("archetype", r.View);
        var keys = r.Cards.Select(c => c.SegmentKey).OrderBy(k => k).ToList();
        Assert.Equal(new[] { "11-0", "11-30", "11-8", "11-9", "12-11", "12-12" }, keys);
        Assert.Equal(r.Cards.OrderByDescending(c => c.Overall).Select(c => c.Overall), r.Cards.Select(c => c.Overall)); // ordenado

        var m8 = r.Cards.Single(c => c.SegmentKey == "11-8");
        Assert.Equal((4, "archetype", 8, true), (m8.Matches, m8.Scoring, m8.Archetype!.Id, m8.Provisional));
        Assert.Equal(new[] { 8, 9, 30 }, m8.Archetypes.Select(a => a.Archetype.Id).OrderBy(i => i)); // uso do jogador segue visível
        Assert.Equal(11L, m8.PlayerEntityId);

        var none = r.Cards.Single(c => c.SegmentKey == "11-0");
        Assert.Null(none.Archetype);
        Assert.Equal(("position", none.Overall), (none.Scoring, none.OverallByPosition));
        Assert.Equal("position", r.Cards.Single(c => c.SegmentKey == "11-30").Scoring); // id fora da tabela

        // cada segmento só usa as partidas dele (gols: 11 marcou nas partidas ímpares)
        Assert.Equal(2, m8.Stats.Goals); // i=1,3
        Assert.Equal(6, r.Cards.Single(c => c.SegmentKey == "12-12").Matches);
        Assert.Equal(8, r.TotalMatches); // total do clube continua o mesmo
    }

    [Fact]
    public async Task ArchetypeViewAppliesMinMatchesAndProvisionalPerSegment()
    {
        using var seed = Seeded();
        var r = await Cards(seed, min: 3, view: "archetype");
        Assert.Equal(new[] { "11-8", "12-12" }, r.Cards.Select(c => c.SegmentKey).OrderBy(k => k));
        Assert.All(r.Cards, c => Assert.True(c.Provisional)); // nenhum segmento chega a 10 jogos

        // 12 jogos no mesmo arquétipo: deixa de ser provisória
        using var big = new AnalyticsSeed();
        for (var i = 0; i < 10; i++) big.Match(T0.AddDays(i), 1, 0, ours: new[] { new Pl(11, Pos: "forward", Arch: 12, Rating: 7.5) });
        var c10 = Assert.Single((await big.Cards().GetCardsAsync(AnalyticsSeed.Club, null, null, null, 3, null, null, "archetype", default)).Cards);
        Assert.False(c10.Provisional);
    }

    [Fact]
    public async Task ArchetypeViewCombinesWithTheFilters()
    {
        using var seed = Seeded();
        var onlyEight = await Cards(seed, arch: 8, view: "archetype");
        Assert.Equal(new[] { "11-8" }, onlyEight.Cards.Select(c => c.SegmentKey));

        var attackers = await Cards(seed, pos: "ATAQUE", view: "archetype");
        Assert.Equal(new[] { "12-11", "12-12" }, attackers.Cards.Select(c => c.SegmentKey).OrderBy(k => k));
        Assert.Equal(new[] { 11, 12 }, attackers.AvailableArchetypes.Select(a => a.Archetype.Id).OrderBy(i => i)); // opções: semântica inalterada

        Assert.Empty((await Cards(seed, arch: 12, pos: "MEIO", view: "archetype")).Cards);
    }

    [Fact]
    public async Task CompareCanScoreEachSideAsASpecificArchetype()
    {
        using var seed = Seeded();
        var svc = seed.Cards();

        var asArchetypes = await svc.GetCompareAsync(AnalyticsSeed.Club, 11, 12, null, null, null, null, 9, 11, null, default);
        Assert.Equal(((int?)9, (int?)11, (int?)null), (asArchetypes!.ArchetypeIdA, asArchetypes.ArchetypeIdB, asArchetypes.ArchetypeId));
        Assert.Equal((2, "archetype", 9), (asArchetypes.A.Matches, asArchetypes.A.Scoring, asArchetypes.A.Archetype!.Id));
        Assert.Equal((2, "archetype", 11), (asArchetypes.B.Matches, asArchetypes.B.Scoring, asArchetypes.B.Archetype!.Id));
        Assert.Equal(2, asArchetypes.RatingSeries.Count(p => p.A != null)); // a série de A só tem as partidas dele como arquétipo 9

        // sem A/B vale archetypeId para os dois lados; A/B têm prioridade
        var shared = await svc.GetCompareAsync(AnalyticsSeed.Club, 11, 12, null, null, null, 8, null, null, null, default);
        Assert.Equal(((int?)8, (int?)8), (shared!.ArchetypeIdA, shared.ArchetypeIdB));
        Assert.Equal((4, 0), (shared.A.Matches, shared.B.Matches));
        var mixed = await svc.GetCompareAsync(AnalyticsSeed.Club, 11, 12, null, null, null, 8, null, 12, null, default);
        Assert.Equal(((int?)8, (int?)12, 4, 6), (mixed!.ArchetypeIdA, mixed.ArchetypeIdB, mixed.A.Matches, mixed.B.Matches));

        // sem nenhum: comportamento de sempre (pesos da posição)
        var plain = await svc.GetCompareAsync(AnalyticsSeed.Club, 11, 12, null, null, null, default);
        Assert.Equal(("position", "position"), (plain!.A.Scoring, plain.B.Scoring));
    }

    [Fact]
    public async Task ControllerValidatesViewAndPerSideArchetypes()
    {
        using var seed = Seeded();
        var c = new PlayerCardsController(seed.Cards());

        var ok = Assert.IsType<OkObjectResult>(await c.Cards(AnalyticsSeed.Club, null, null, null, "1", default, view: "ARCHETYPE"));
        Assert.Equal("archetype", Assert.IsType<PlayerCardsDto>(ok.Value).View);
        Assert.Equal("player", Assert.IsType<PlayerCardsDto>(Assert.IsType<OkObjectResult>(await c.Cards(AnalyticsSeed.Club, null, null, null, "1", default, view: "")).Value).View);
        foreach (var bad in new[] { "cards", "x", "1" })
        {
            var r = Assert.IsType<ObjectResult>(await c.Cards(AnalyticsSeed.Club, null, null, null, "1", default, view: bad));
            Assert.Equal(400, r.StatusCode);
            Assert.Contains("view", Assert.IsType<ProblemDetails>(r.Value).Detail);
        }

        Assert.IsType<OkObjectResult>(await c.Compare(AnalyticsSeed.Club, "11", "12", null, null, null, default, archetypeA: "9", archetypeB: "11"));
        foreach (var bad in new[] { "0", "256", "x" })
        {
            Assert.Equal(400, ((ObjectResult)await c.Compare(AnalyticsSeed.Club, "11", "12", null, null, null, default, archetypeA: bad)).StatusCode);
            Assert.Equal(400, ((ObjectResult)await c.Compare(AnalyticsSeed.Club, "11", "12", null, null, null, default, archetypeB: bad)).StatusCode);
        }
    }
    // ------------------------------------------------------------------ filtro de jogador

    private static Task<PlayerCardsDto> CardsOf(
        AnalyticsSeed seed, long? player, int min = 1, int? arch = null, string? pos = null, string view = "player") =>
        seed.Cards().GetCardsAsync(AnalyticsSeed.Club, null, null, null, min, arch, pos, view, player, default);

    [Fact]
    public async Task PlayerFilterKeepsOnlyThatPlayersCardsAndSegments()
    {
        using var seed = Seeded();
        var r = await CardsOf(seed, 11);
        Assert.Equal(11L, r.PlayerEntityId);
        Assert.Equal(11L, Assert.Single(r.Cards).PlayerEntityId);
        Assert.Null((await CardsOf(seed, null)).PlayerEntityId);

        var seg = await CardsOf(seed, 12, view: "archetype");
        Assert.Equal(new[] { "12-11", "12-12" }, seg.Cards.Select(c => c.SegmentKey).OrderBy(k => k));
        Assert.Equal(8, seg.TotalMatches);

        // AND com posição/arquétipo
        Assert.Empty((await CardsOf(seed, 11, pos: "ATAQUE")).Cards);
        Assert.Equal(4, Assert.Single((await CardsOf(seed, 11, arch: 8)).Cards).Matches);
        // jogador inexistente: grade vazia, sem erro
        Assert.Empty((await CardsOf(seed, 999)).Cards);
    }

    [Fact]
    public async Task SelectedPlayerIgnoresMinMatches()
    {
        using var seed = Seeded();
        Assert.Empty((await CardsOf(seed, null, min: 20)).Cards);                   // sem jogador: min esconde todos
        Assert.Equal(11L, Assert.Single((await CardsOf(seed, 11, min: 20)).Cards).PlayerEntityId); // com jogador: aparece

        var seg = await CardsOf(seed, 12, min: 5, view: "archetype");              // inclui o segmento de 2 jogos
        Assert.Equal(new[] { "12-11", "12-12" }, seg.Cards.Select(c => c.SegmentKey).OrderBy(k => k));
        Assert.Equal(new[] { "12-12" }, (await CardsOf(seed, null, min: 5, view: "archetype")).Cards.Select(c => c.SegmentKey)); // sem jogador: min vale
    }

    [Fact]
    public async Task AvailableOptionsFollowThePlayerAndPlayersIgnoreIt()
    {
        using var seed = Seeded();

        // sem filtros: todos os jogadores (por nome), todos os arquétipos e posições
        var all = await CardsOf(seed, null);
        Assert.Equal(new long[] { 11, 12 }, all.AvailablePlayers.Select(p => p.PlayerEntityId));
        Assert.Equal(("Jogador 11", 8), (all.AvailablePlayers[0].Name, all.AvailablePlayers[0].Matches));
        Assert.Equal(new[] { 8, 9, 11, 12, 30 }, all.AvailableArchetypes.Select(a => a.Archetype.Id).OrderBy(i => i));
        Assert.Equal(new[] { "ATAQUE", "MEIO" }, all.AvailablePositionGroups.Select(g => g.PositionGroup).OrderBy(g => g));

        // jogador escolhido: a lista de jogadores NÃO encolhe; arquétipos e posições são só os dele
        var p11 = await CardsOf(seed, 11);
        Assert.Equal(new long[] { 11, 12 }, p11.AvailablePlayers.Select(p => p.PlayerEntityId));
        Assert.Equal(new[] { 8, 9, 30 }, p11.AvailableArchetypes.Select(a => a.Archetype.Id).OrderBy(i => i));
        Assert.Equal(new[] { ("MEIO", 8) }, p11.AvailablePositionGroups.Select(g => (g.PositionGroup, g.Matches)));

        // posição aplicada: a lista de jogadores IGNORA posição/arquétipo (dá para trocar direto para um meia)
        var ataque = await CardsOf(seed, null, pos: "ATAQUE");
        Assert.Equal(new long[] { 11, 12 }, ataque.AvailablePlayers.Select(p => p.PlayerEntityId));
        Assert.Equal(new[] { 8, 8 }, ataque.AvailablePlayers.Select(p => p.Matches)); // total de linhas, não só as de ataque
        Assert.Empty((await CardsOf(seed, 11, pos: "ATAQUE")).AvailableArchetypes); // 11 nunca jogou de atacante
        Assert.Equal(new[] { 11, 12 }, (await CardsOf(seed, 12, pos: "ATAQUE")).AvailableArchetypes.Select(a => a.Archetype.Id).OrderBy(i => i));

        // arquétipo aplicado: jogadores continuam todos; NÃO nas opções de arquétipo
        var arch12 = await CardsOf(seed, 12, arch: 12);
        Assert.Equal(new long[] { 11, 12 }, arch12.AvailablePlayers.Select(p => p.PlayerEntityId));
        Assert.Equal(8, arch12.AvailablePlayers[1].Matches);
        Assert.Equal(new[] { 11, 12 }, arch12.AvailableArchetypes.Select(a => a.Archetype.Id).OrderBy(i => i));
    }

    [Fact]
    public async Task ControllerValidatesPlayerEntityId()
    {
        using var seed = Seeded();
        var c = new PlayerCardsController(seed.Cards());
        var ok = Assert.IsType<PlayerCardsDto>(Assert.IsType<OkObjectResult>(
            await c.Cards(AnalyticsSeed.Club, null, null, null, "1", default, playerEntityId: "11")).Value);
        Assert.Equal(11L, ok.PlayerEntityId);
        Assert.IsType<OkObjectResult>(await c.Cards(AnalyticsSeed.Club, null, null, null, "1", default, playerEntityId: ""));
        foreach (var bad in new[] { "0", "-3", "x", "1.5" })
        {
            var r = Assert.IsType<ObjectResult>(await c.Cards(AnalyticsSeed.Club, null, null, null, "1", default, playerEntityId: bad));
            Assert.Equal(400, r.StatusCode);
            Assert.Contains("playerEntityId", Assert.IsType<ProblemDetails>(r.Value).Detail);
        }
    }
    // ------------------------------------------------------------------ Regularidade sem presença

    [Fact]
    public async Task RegularityIgnoresAttendanceAndSplittingIntoArchetypesKeepsIt()
    {
        using var seed = new AnalyticsSeed();
        for (var i = 0; i < 20; i++)
        {
            var rating = i % 2 == 0 ? 6.0 : 8.0; // desvio 1,0 em qualquer recorte par
            var ours = new List<Pl> { new Pl(11, Rating: rating, Pos: "midfielder", Arch: i < 10 ? 8 : 9) };
            if (i == 0 || i == 9 || i == 19) ours.Add(new Pl(12, Rating: 7.0, Pos: "midfielder", Arch: 8)); // 3 de 20 jogos
            if (i < 3) ours.Add(new Pl(13, Rating: 7.0, Pos: "midfielder", Arch: 8));                       // 3 jogos seguidos
            seed.Match(T0.AddDays(i), 1, 0, ours: ours.ToArray());
        }

        var player = await Cards(seed);
        // presença muito diferente (3/20 espalhados x 3 seguidos), mesma nota estável e mesma amostra: mesma Regularidade
        Assert.Equal(player.Cards.Single(c => c.PlayerEntityId == 13).Axes.Reg, player.Cards.Single(c => c.PlayerEntityId == 12).Axes.Reg);
        // quem joga sempre com nota instável fica abaixo de quem joga pouco com nota estável (presença não compensa)
        Assert.True(player.Cards.Single(c => c.PlayerEntityId == 12).Axes.Reg > player.Cards.Single(c => c.PlayerEntityId == 11).Axes.Reg);

        // dividir o jogador em arquétipos não derruba a Regularidade (só o tamanho da amostra pesa um pouco)
        var combined = player.Cards.Single(c => c.PlayerEntityId == 11).Axes.Reg;
        var segments = (await Cards(seed, view: "archetype")).Cards.Where(c => c.PlayerEntityId == 11).ToList();
        Assert.Equal(2, segments.Count);
        Assert.All(segments, c => Assert.InRange(c.Axes.Reg - combined, -1, 4));
        var filtered = Assert.Single((await Cards(seed, arch: 8)).Cards, c => c.PlayerEntityId == 11);
        Assert.InRange(filtered.Axes.Reg - combined, -1, 4);
    }
    // ------------------------------------------------------------------ v3: bônus de impacto, curva e teto de provisória

    [Fact]
    public void ImpactBonusKeepsEveryGroupAndArchetypeRowAtOneAndAddsTenPoints()
    {
        foreach (var group in ArchetypeGroups.All)
        {
            var b = CardScoring.BaseWeights(group);
            var w = CardScoring.Weights(group);
            Assert.True(Math.Abs(w.Ata + w.Cri + w.Pas + w.Def + w.Imp + w.Reg + w.Gol - 1.0) < 1e-9, group);
            Assert.Equal(b.Imp + CardScoring.ImpWeightBonus, w.Imp, 9);

            foreach (var id in CardScoring.ArchetypeWeights.Keys)
            {
                var a = CardScoring.Weights(group, id);
                Assert.True(Math.Abs(a.Ata + a.Cri + a.Pas + a.Def + a.Imp + a.Reg + a.Gol - 1.0) < 1e-9, $"{id} x {group}");
                if (CardScoring.UsesArchetypeWeights(id, group == CardScoring.GroupKeeper))
                    Assert.Equal(CardScoring.ArchetypeWeights[id].Imp + 0.10, a.Imp, 9);
                else
                    Assert.Equal(w, a); // incompatível/goleiro x linha: pesos da posição
            }
        }
    }

    [Fact]
    public void SaturatingCurveIsConcaveMonotonicAndHitsTheDocumentedPoints()
    {
        Assert.Equal(0, CardScoring.Saturate(0, 0.8), 9);
        Assert.Equal(85.0, CardScoring.Saturate(0.8, 0.8), 1);   // 0,8 gol/jogo ~ 85, não satura
        Assert.Equal(99, CardScoring.Saturate(1.12, 0.8), 9);    // t = tMax
        Assert.Equal(99, CardScoring.Saturate(5, 0.8), 9);       // trava
        Assert.True(CardScoring.Saturate(0.4, 0.8) > 99 * 0.4 / 0.8); // côncava: acima da reta
        var last = -1.0;
        for (var x = 0.0; x <= 1.3; x += 0.05) { var v = CardScoring.Saturate(x, 0.8); Assert.True(v >= last); last = v; }
    }

    [Fact]
    public async Task ProvisionalCardsNeverReachEliteButSettledOnesCan()
    {
        using var seed = new AnalyticsSeed();
        for (var i = 0; i < 12; i++)
            seed.Match(T0.AddDays(i), 3, 0, ours: new[]
            {
                new Pl(11, Goals: 3, Assists: 2, Rating: 9.6, Mom: true, Pos: "forward", Arch: 12),
                new Pl(12, Goals: 3, Assists: 2, Rating: 9.6, Mom: true, Pos: "forward", Arch: 12),
            });
        foreach (var mp in seed.Db.MatchPlayers.Local) { mp.Shots = 3; mp.Passattempts = 30; mp.Passesmade = 28; mp.SecondsPlayed = 5400; }
        seed.Db.SaveChanges();

        var settled = Assert.Single((await Cards(seed)).Cards, c => c.PlayerEntityId == 11);
        Assert.False(settled.Provisional);
        Assert.Equal("elite", settled.Tier);
        Assert.True(settled.Overall >= 85);

        // mesmas estatísticas, 6 jogos: provisória -> teto de 84 nos dois overalls (e faixa ouro)
        var short6 = await seed.Cards().GetCardsAsync(AnalyticsSeed.Club, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 6), null, 1, null, null, "player", default);
        var p = Assert.Single(short6.Cards, c => c.PlayerEntityId == 11);
        Assert.True(p.Provisional);
        Assert.Equal((84, "ouro"), (p.Overall, p.Tier));
        Assert.Equal(84, p.OverallByPosition);

        var seg = (await Cards(seed, arch: 12, view: "archetype")).Cards.Single(c => c.PlayerEntityId == 11);
        Assert.Equal(CardScoring.ProvisionalOverallCap, 84);
        Assert.False(seg.Provisional); // 12 jogos no arquétipo: sem teto
    }
}
