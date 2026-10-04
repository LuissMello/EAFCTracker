using EAFCMatchTracker.Application.Services.Analytics;
using Xunit;

namespace EAFCMatchTracker.UnitTests;

public class CardScoringTests
{
    private const int P = 6; // precisão das comparações de ponto flutuante

    // ------------------------------------------------------------------ scale

    [Theory]
    [InlineData(0, 0, 1.5, 0)]
    [InlineData(1.5, 0, 1.5, 99)]
    [InlineData(0.75, 0, 1.5, 49.5)]
    [InlineData(-3, 0, 1.5, 0)]      // abaixo do piso: trava em 0
    [InlineData(10, 0, 1.5, 99)]     // acima do teto: trava em 99
    [InlineData(5.5, 5.5, 9.0, 0)]
    [InlineData(9.0, 5.5, 9.0, 99)]
    [InlineData(7.25, 5.5, 9.0, 49.5)]
    public void ScaleClampsToZeroAndNinetyNine(double x, double lo, double hi, double expected) =>
        Assert.Equal(expected, CardScoring.Scale(x, lo, hi), P);

    [Fact]
    public void ScaleTreatsNaNAsZero() => Assert.Equal(0, CardScoring.Scale(double.NaN, 0, 1));

    // ------------------------------------------------------------------ eixos (escalas v2)

    [Theory]
    [InlineData(0, 25, 0)]
    [InlineData(0.8, 55, 99)]
    [InlineData(0.4, 40, 49.5)]
    [InlineData(3, 200, 99)]        // clamps
    [InlineData(-1, 0, 0)]
    [InlineData(0.8, 25, 59.4)]     // só os gols: 0,6 x 99
    [InlineData(0, 55, 39.6)]       // só a precisão: 0,4 x 99
    public void AtaMixesGoalsPerMatchAndShotAccuracy(double gpm, double acc, double expected) =>
        Assert.Equal(expected, CardScoring.RawAta(gpm, acc), P);

    [Theory]
    [InlineData(40, 5, 0)]
    [InlineData(80, 25, 99)]
    [InlineData(60, 15, 49.5)]
    [InlineData(80, 5, 69.3)]       // 0,7 x 99
    [InlineData(40, 25, 29.7)]      // 0,3 x 99
    [InlineData(100, 100, 99)]
    [InlineData(0, 0, 0)]
    public void PasMixesAccuracyAndVolume(double acc, double perMatch, double expected) =>
        Assert.Equal(expected, CardScoring.RawPas(acc, perMatch), P);

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(0.8, 0, 99)]
    [InlineData(0.4, 0, 49.5)]
    [InlineData(0.2, 0.4, 49.5)]     // 0,2 + 0,5 x 0,4 = 0,4
    [InlineData(0, 1.6, 99)]         // só pré-assistências: 0,5 x 1,6 = 0,8
    [InlineData(2, 0, 99)]
    public void CriCreditsHalfAPreAssist(double assists, double pre, double expected) =>
        Assert.Equal(expected, CardScoring.RawCri(assists, pre), P);

    [Theory]
    [InlineData(10, 0, 0)]
    [InlineData(75, 3, 99)]
    [InlineData(42.5, 1.5, 49.5)]
    [InlineData(75, 0, 59.4)]
    [InlineData(10, 3, 39.6)]
    [InlineData(0, 20, 39.6)]
    public void DefMixesTackleAccuracyAndVolume(double acc, double made, double expected) =>
        Assert.Equal(expected, CardScoring.RawDef(acc, made), P);

    [Theory]
    [InlineData(4.5, 0, 0)]
    [InlineData(8.5, 0.3, 99)]
    [InlineData(6.5, 0.15, 49.5)]
    [InlineData(8.5, 0, 69.3)]
    [InlineData(4.5, 0.3, 29.7)]
    [InlineData(10, 1, 99)]
    public void ImpMixesAverageRatingAndMotmRate(double rating, double motmRate, double expected) =>
        Assert.Equal(expected, CardScoring.RawImp(rating, motmRate), P);

    [Theory]
    [InlineData(1.0, 0.0, 10, 99)]       // presença total e nota estável
    [InlineData(0.5, 0.75, 10, 49.5)]
    [InlineData(1.0, 1.5, 10, 49.5)]     // desvio no teto: metade da estabilidade zera
    [InlineData(1.0, 2.0, 10, 49.5)]     // acima do teto: trava
    [InlineData(0.0, 0.0, 5, 49.5)]      // nunca presente
    [InlineData(1.0, 0.0, 1, 74.5)]      // 1 jogo: estabilidade neutra (50)
    [InlineData(1.0, 1.4, 1, 74.5)]      // o desvio é ignorado com 1 jogo
    [InlineData(2.0, 0.0, 10, 99)]       // presença > 100% é travada
    public void RegMixesAttendanceAndRatingStability(double attendance, double stdDev, int matches, double expected) =>
        Assert.Equal(expected, CardScoring.RawReg(attendance, stdDev, matches), P);

    [Theory]
    [InlineData(40, 3, 0)]
    [InlineData(85, 0, 99)]
    [InlineData(62.5, 1.5, 49.5)]
    [InlineData(85, 3, 59.4)]
    [InlineData(40, 0, 39.6)]
    [InlineData(100, 5, 59.4)]           // gols sofridos acima do teto: trava
    public void GolMixesSavePctAndGoalsConceded(double savePct, double conceded, double expected) =>
        Assert.Equal(expected, CardScoring.RawGol(savePct, conceded), P);

    // ------------------------------------------------------------------ encolhimento e alongamento

    [Theory]
    [InlineData(100, 1, 62.5)]           // (1x100 + 3x50) / 4
    [InlineData(100, 5, 81.25)]          // (5x100 + 150) / 8
    [InlineData(100, 10, 88.461538)]     // (10x100 + 150) / 13
    [InlineData(100, 50, 97.169811)]     // (50x100 + 150) / 53
    [InlineData(0, 1, 37.5)]
    [InlineData(0, 50, 2.830189)]
    [InlineData(50, 7, 50)]              // neutro continua neutro
    [InlineData(80, 0, 50)]              // sem jogos: tudo neutro
    public void ShrinkPullsTowardFiftyWithThreeMatchesOfPrior(double raw, int matches, double expected) =>
        Assert.Equal(expected, CardScoring.Shrink(raw, matches), 4);

    [Theory]
    [InlineData(50, 50)]                 // neutro continua neutro
    [InlineData(60, 64)]                 // 50 + 1,4 x 10
    [InlineData(40, 36)]
    [InlineData(100, 99)]                // trava em 99
    [InlineData(0, 0)]                   // trava em 0 (50 - 70 < 0)
    [InlineData(85, 99)]                 // 50 + 1,4 x 35 = 99
    public void StretchWidensAroundFiftyAndClamps(double x, double expected) =>
        Assert.Equal(expected, CardScoring.Stretch(x), P);

    [Fact]
    public void ScoreAppliesShrinkThenStretch()
    {
        Assert.Equal(50, CardScoring.Score(60, 0), P);                         // sem jogos: neutro
        Assert.Equal(69.6, CardScoring.Score(70, 7), P);                       // shrink (7x70+150)/10 = 64 -> 50 + 1,4 x 14
        Assert.Equal(99, CardScoring.Score(100, 10), P);                       // 88,46 -> 103,8 -> trava
        Assert.True(CardScoring.Score(30, 2) > CardScoring.Score(30, 30));     // poucos jogos ficam mais perto de 50
        Assert.True(CardScoring.Score(80, 2) < CardScoring.Score(80, 30));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(9, true)]
    [InlineData(10, false)]
    [InlineData(40, false)]
    public void ProvisionalBelowTenMatches(int matches, bool expected) =>
        Assert.Equal(expected, CardScoring.IsProvisional(matches));

    // ------------------------------------------------------------------ calibração com dados reais do clube

    /// <summary>
    /// Perfis do clube (prints do usuário). O que não aparece nos prints é ASSUMIDO: passes/jogo, desarmes/jogo, pré-assistências,
    /// presença no período (clube com ~25 jogos) e desvio-padrão das notas. Sem esses chutes não há como fechar o overall.
    /// </summary>
    private static (int Overall, string Tier) Profile(
        string group, int m, double gpm, double shot, double pass, double ppm, double apm, double pre,
        double tk, double tkm, double rating, double motm, double attendance, double sd)
    {
        var axes = new CardAxes(
            CardScoring.Score(CardScoring.RawAta(gpm, shot), m),
            CardScoring.Score(CardScoring.RawPas(pass, ppm), m),
            CardScoring.Score(CardScoring.RawCri(apm, pre), m),
            CardScoring.Score(CardScoring.RawDef(tk, tkm), m),
            CardScoring.Score(CardScoring.RawImp(rating, motm), m),
            CardScoring.Score(CardScoring.RawReg(attendance, sd, m), m), null);
        var overall = CardScoring.Overall(group, axes);
        return (overall, CardScoring.Tier(overall));
    }

    [Fact]
    public void RealClubProfilesLandInTheExpectedTiers()
    {
        var lMello = Profile("ATAQUE", 18, 0.61, 52.4, 69.5, 25, 0.39, 0.15, 21.6, 1.5, 7.9, 3 / 18.0, 18 / 25.0, 0.7);
        var luska = Profile("ATAQUE", 14, 0.64, 69.2, 75.4, 28, 0.36, 0.15, 82.4, 2.0, 8.3, 3 / 14.0, 14 / 25.0, 0.6);
        var zaga = Profile("MEIO", 13, 0.38, 33.3, 67.6, 30, 0.62, 0.2, 31.6, 3.0, 7.4, 0, 13 / 25.0, 0.8);
        var pedro = Profile("MEIO", 2, 0.5, 50, 72, 28, 0.5, 0.1, 40, 3.0, 6.95, 0, 2 / 25.0, 0.65);

        Assert.InRange(lMello.Overall, 75, 80);
        Assert.Equal("ouro", lMello.Tier);
        Assert.InRange(luska.Overall, 80, 84);
        Assert.Equal("ouro", luska.Tier);
        Assert.InRange(zaga.Overall, 68, 72);
        Assert.Equal("prata", zaga.Tier);
        Assert.InRange(pedro.Overall, 55, 64);   // provisório: puxado para o neutro
        Assert.True(CardScoring.IsProvisional(2));
    }

    [Fact]
    public void EliteNeedsAnExceptionalProfileAndWeakProfilesStayBronze()
    {
        // nota média 8,5 e ~1 gol por jogo, boa pontaria e presença: elite
        var star = Profile("ATAQUE", 25, 1.0, 60, 78, 28, 0.4, 0.15, 50, 2.5, 8.5, 0.3, 0.85, 0.6);
        Assert.Equal("elite", star.Tier);
        Assert.InRange(star.Overall, 85, 95);

        // o Luska (8,3 e 0,64 gol/jogo) é ouro alto, não elite
        var luska = Profile("ATAQUE", 14, 0.64, 69.2, 75.4, 28, 0.36, 0.15, 82.4, 2.0, 8.3, 3 / 14.0, 0.56, 0.6);
        Assert.NotEqual("elite", luska.Tier);

        // média 7,0 sem muita participação: no máximo prata baixa
        var average = Profile("MEIO", 20, 0.3, 40, 72, 25, 0.25, 0.1, 45, 2.5, 7.0, 0.05, 0.6, 0.8);
        Assert.InRange(average.Overall, 50, 62);

        // nota < 6,3 e quase sem participação: bronze
        var weak = Profile("MEIO", 12, 0.05, 20, 60, 18, 0.1, 0, 25, 1, 6.1, 0, 0.25, 1.0);
        Assert.Equal("bronze", weak.Tier);
        Assert.True(weak.Overall < 45);

        // zagueiro sólido (82% de passes, 65% de desarmes, nota 7,3): prata alta/ouro
        var defender = Profile("DEFESA", 22, 0.05, 20, 82, 30, 0.15, 0.1, 65, 4, 7.3, 0.05, 0.8, 0.7);
        Assert.InRange(defender.Overall, 66, 80);
    }

    // ------------------------------------------------------------------ posição

    [Theory]
    [InlineData("forward", "ATAQUE")]
    [InlineData("Forward", "ATAQUE")]
    [InlineData("striker", "ATAQUE")]
    [InlineData("winger", "ATAQUE")]
    [InlineData("ST", "ATAQUE")]
    [InlineData("lw", "ATAQUE")]
    [InlineData("centroavante", "ATAQUE")]
    [InlineData("midfielder", "MEIO")]
    [InlineData("CAM", "MEIO")]
    [InlineData("cdm", "MEIO")]
    [InlineData("defender", "DEFESA")]
    [InlineData("fullback", "DEFESA")]
    [InlineData("centreback", "DEFESA")]
    [InlineData("centerback", "DEFESA")]
    [InlineData("wingback", "DEFESA")]   // contém "wing", mas é defesa
    [InlineData("CB", "DEFESA")]
    [InlineData("rwb", "DEFESA")]
    [InlineData("zagueiro", "DEFESA")]
    [InlineData("goalkeeper", "GOLEIRO")]
    [InlineData("GK", "GOLEIRO")]
    [InlineData("goleiro", "GOLEIRO")]
    [InlineData("  goalkeeper  ", "GOLEIRO")]
    [InlineData("", "MEIO")]
    [InlineData("   ", "MEIO")]
    [InlineData(null, "MEIO")]
    [InlineData("whatever", "MEIO")]     // desconhecida vira MEIO
    public void PositionGroupMapsEaPositionsRobustly(string? position, string expected) =>
        Assert.Equal(expected, CardScoring.PositionGroup(position));

    // ------------------------------------------------------------------ overall

    [Theory]
    [InlineData("ATAQUE")]
    [InlineData("MEIO")]
    [InlineData("DEFESA")]
    [InlineData("GOLEIRO")]
    [InlineData("qualquer")]
    public void WeightsSumToOne(string group)
    {
        var w = CardScoring.Weights(group);
        Assert.Equal(1.0, w.Ata + w.Cri + w.Pas + w.Def + w.Imp + w.Reg + w.Gol, 9);
    }

    [Fact]
    public void WeightsMatchTheSpecPerPosition()
    {
        Assert.Equal((0.35, 0.15, 0.10, 0.05, 0.25, 0.10, 0.0), CardScoring.Weights("ATAQUE"));
        Assert.Equal((0.10, 0.25, 0.25, 0.15, 0.15, 0.10, 0.0), CardScoring.Weights("MEIO"));
        Assert.Equal((0.05, 0.05, 0.15, 0.40, 0.20, 0.15, 0.0), CardScoring.Weights("DEFESA"));
        Assert.Equal((0.0, 0.0, 0.10, 0.0, 0.25, 0.15, 0.50), CardScoring.Weights("GOLEIRO"));
    }

    [Fact]
    public void OverallIsTheRoundedWeightedSumPerPosition()
    {
        // ATAQUE: .35x80 + .15x40 + .10x60 + .05x20 + .25x70 + .10x50 = 63,5 -> 64
        Assert.Equal(64, CardScoring.Overall("ATAQUE", new CardAxes(80, 60, 40, 20, 70, 50, null)));
        // MEIO: .25x80 + .25x60 + .10x40 + .15x20 + .15x70 + .10x50 = 57,5 -> 58
        Assert.Equal(58, CardScoring.Overall("MEIO", new CardAxes(40, 60, 80, 20, 70, 50, null)));
        // DEFESA: .40x80 + .15x60 + .20x70 + .15x50 + .05x40 + .05x20 = 65,5 -> 66
        Assert.Equal(66, CardScoring.Overall("DEFESA", new CardAxes(20, 60, 40, 80, 70, 50, null)));
        // GOLEIRO: .50x80 + .25x70 + .15x50 + .10x60 = 71
        Assert.Equal(71, CardScoring.Overall("GOLEIRO", new CardAxes(null, 60, 0, 0, 70, 50, 80)));
    }

    [Fact]
    public void OverallIsClampedToOneAndNinetyNine()
    {
        Assert.Equal(99, CardScoring.Overall("ATAQUE", new CardAxes(100, 100, 100, 100, 100, 100, null)));
        Assert.Equal(1, CardScoring.Overall("MEIO", new CardAxes(0, 0, 0, 0, 0, 0, null)));
        Assert.Equal(1, CardScoring.Overall("GOLEIRO", new CardAxes(null, 0, 0, 0, 0, 0, 0)));
    }

    [Fact]
    public void GoalkeeperUsesGolInsteadOfAta()
    {
        // ATA altíssimo não conta para o goleiro (peso 0); GOL pesa 50%.
        var withHugeAta = CardScoring.Overall("GOLEIRO", new CardAxes(99, 50, 50, 50, 50, 50, 60));
        var withoutAta = CardScoring.Overall("GOLEIRO", new CardAxes(null, 50, 50, 50, 50, 50, 60));
        Assert.Equal(withoutAta, withHugeAta);

        // E o GOL de um jogador de linha (peso 0) também não conta.
        Assert.Equal(CardScoring.Overall("ATAQUE", new CardAxes(70, 50, 50, 50, 50, 50, null)),
            CardScoring.Overall("ATAQUE", new CardAxes(70, 50, 50, 50, 50, 50, 99)));

        // Mexer no GOL mexe no overall do goleiro: +20 de GOL = +10 de overall.
        Assert.Equal(10, CardScoring.Overall("GOLEIRO", new CardAxes(null, 50, 50, 50, 50, 50, 80))
                         - CardScoring.Overall("GOLEIRO", new CardAxes(null, 50, 50, 50, 50, 50, 60)));
    }

    [Theory]
    [InlineData(1, "bronze")]
    [InlineData(59, "bronze")]
    [InlineData(60, "prata")]
    [InlineData(74, "prata")]
    [InlineData(75, "ouro")]
    [InlineData(84, "ouro")]
    [InlineData(85, "elite")]
    [InlineData(99, "elite")]
    public void TierThresholds(int overall, string expected) => Assert.Equal(expected, CardScoring.Tier(overall));
}
