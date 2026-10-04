namespace EAFCMatchTracker.Application.Services.Analytics;

/// <summary>Eixos de uma carta (0..99). <c>Ata</c> é nulo para goleiros e <c>Gol</c> só existe para goleiros.</summary>
public readonly record struct CardAxes(double? Ata, double Pas, double Cri, double Def, double Imp, double Reg, double? Gol);

/// <summary>
/// Pontuação das cartas ("Cartas"): escalas ABSOLUTAS (um elenco pode ter só ~4 jogadores ativos, então nada de percentil),
/// encolhimento para 50 em amostras pequenas, overall ponderado por posição e faixa (tier). Classe pura: todas as
/// constantes e fórmulas ficam aqui para serem testadas.
///
/// CALIBRAÇÃO (v2). As escalas v1 (gols/jogo 0–1,5; chutes 30–80%; passes 55–92%, volume 5–40; criação 0–1,2; desarmes 25–85%
/// e 0–8/jogo; nota 5,5–9,0; MOTM 0–0,5; encolhimento k=5) eram boas para jogadores de "11 contra 11", mas um elenco de
/// Pro Clubs (times de ~4–5 jogadores, ~2 gols por jogo) nunca as alcançava: um jogador excelente fechava em ~62 e ninguém
/// chegava a ouro/elite. Referências reais do clube usadas para calibrar (ver PlayerCardService/testes de calibração):
///   L. Mello 18j 0,61 g/j 52% chutes 70% passes nota 7,9 -> ouro (~75–80); Luska 14j 0,64 g/j 69% chutes 75% passes
///   82% desarmes nota 8,3 -> ouro alto (~80–84); Zaga 13j 0,38 g/j 0,62 a/j nota 7,4 -> prata alta (~68–72);
///   Pedro 2j (7,6 e 6,3) -> provisório, bronze alto/prata baixa; elite só com nota ~8,5+ e ~1 gol/jogo.
/// Mudanças: escalas mais estreitas (valores em Const abaixo), k de 5 para 3 e um alongamento por eixo em torno de 50
/// (<see cref="AxisStretch"/> = 1,4: 50 continua neutro, bons eixos sobem, ruins descem). As fórmulas, os pesos por posição,
/// a regra de provisória (&lt; 10 jogos) e o contrato JSON não mudam; os limiares de faixa (60/75/85) também não.
/// </summary>
public static class CardScoring
{
    public const double MaxScore = 99;

    // --- limites das escalas (lo, hi) de cada métrica
    public const double GoalsPerMatchLo = 0, GoalsPerMatchHi = 0.8;          // v1: 1,5
    public const double ShotAccuracyLo = 25, ShotAccuracyHi = 55;            // v1: 30–80
    public const double PassAccuracyLo = 40, PassAccuracyHi = 80;            // v1: 55–92
    public const double PassesPerMatchLo = 5, PassesPerMatchHi = 25;         // v1: 5–40
    public const double CreationLo = 0, CreationHi = 0.8;                    // v1: 1,2
    public const double PreAssistCreditWeight = 0.5;
    public const double TackleAccuracyLo = 10, TackleAccuracyHi = 75;        // v1: 25–85
    public const double TacklesPerMatchLo = 0, TacklesPerMatchHi = 3;        // v1: 8
    public const double RatingLo = 4.5, RatingHi = 8.5;                      // v1: 5,5–9,0
    public const double MotmRateLo = 0, MotmRateHi = 0.3;                    // v1: 0,5
    public const double RatingStdDevCeiling = 1.5;
    public const double SavePctLo = 40, SavePctHi = 85;
    public const double ConcededCeiling = 3;

    // --- pesos dentro de cada eixo
    public const double AtaGoalsWeight = 0.6, AtaShotsWeight = 0.4;
    public const double PasAccuracyWeight = 0.7, PasVolumeWeight = 0.3;
    public const double DefAccuracyWeight = 0.6, DefVolumeWeight = 0.4;
    public const double ImpRatingWeight = 0.7, ImpMotmWeight = 0.3;
    public const double RegAttendanceWeight = 0.5, RegStabilityWeight = 0.5;
    public const double GolSaveWeight = 0.6, GolConcededWeight = 0.4;

    // --- amostra pequena
    /// <summary>Valor neutro para o qual os eixos são puxados quando há poucos jogos.</summary>
    public const double NeutralScore = 50;
    /// <summary>Peso (em jogos) do valor neutro no encolhimento: score = (M·raw + 3·50) / (M + 3). v1: 5.</summary>
    public const double ShrinkPriorMatches = 3;
    /// <summary>Alongamento do eixo (já encolhido) em torno de 50: 50 + 1,4·(x − 50), limitado a 0..99. 1,0 = sem alongamento.</summary>
    public const double AxisStretch = 1.4;
    /// <summary>Carta provisória: menos de 10 jogos.</summary>
    public const int ProvisionalBelow = 10;

    // --- grupos de posição
    public const string GroupAttack = "ATAQUE";
    public const string GroupMidfield = "MEIO";
    public const string GroupDefense = "DEFESA";
    public const string GroupKeeper = "GOLEIRO";

    // --- faixas
    public const string TierBronze = "bronze", TierSilver = "prata", TierGold = "ouro", TierElite = "elite";
    public const int SilverFrom = 60, GoldFrom = 75, EliteFrom = 85;

    /// <summary>clamp((x − lo) / (hi − lo), 0, 1) × 99.</summary>
    public static double Scale(double x, double lo, double hi)
    {
        if (double.IsNaN(x)) return 0;
        var t = (x - lo) / (hi - lo);
        return Math.Clamp(t, 0, 1) * MaxScore;
    }

    /// <summary>Encolhimento rumo a 50 para amostras pequenas: (M·raw + 3·50) / (M + 3).</summary>
    public static double Shrink(double raw, int matches)
    {
        var m = Math.Max(0, matches);
        return (m * raw + ShrinkPriorMatches * NeutralScore) / (m + ShrinkPriorMatches);
    }

    /// <summary>Alongamento em torno de 50 (neutro fica neutro), limitado a 0..99.</summary>
    public static double Stretch(double x) => Math.Clamp(NeutralScore + AxisStretch * (x - NeutralScore), 0, MaxScore);

    /// <summary>Nota final do eixo: valor bruto -> encolhimento por amostra pequena -> alongamento.</summary>
    public static double Score(double raw, int matches) => Stretch(Shrink(raw, matches));

    public static bool IsProvisional(int matches) => matches < ProvisionalBelow;

    // ------------------------------------------------------------------ eixos brutos (antes do encolhimento)

    public static double RawAta(double goalsPerMatch, double shotAccuracyPct) =>
        AtaGoalsWeight * Scale(goalsPerMatch, GoalsPerMatchLo, GoalsPerMatchHi)
        + AtaShotsWeight * Scale(shotAccuracyPct, ShotAccuracyLo, ShotAccuracyHi);

    public static double RawPas(double passAccuracyPct, double passesPerMatch) =>
        PasAccuracyWeight * Scale(passAccuracyPct, PassAccuracyLo, PassAccuracyHi)
        + PasVolumeWeight * Scale(passesPerMatch, PassesPerMatchLo, PassesPerMatchHi);

    public static double RawCri(double assistsPerMatch, double preAssistsPerMatch) =>
        Scale(assistsPerMatch + PreAssistCreditWeight * preAssistsPerMatch, CreationLo, CreationHi);

    public static double RawDef(double tackleAccuracyPct, double tacklesMadePerMatch) =>
        DefAccuracyWeight * Scale(tackleAccuracyPct, TackleAccuracyLo, TackleAccuracyHi)
        + DefVolumeWeight * Scale(tacklesMadePerMatch, TacklesPerMatchLo, TacklesPerMatchHi);

    public static double RawImp(double avgRating, double motmRate) =>
        ImpRatingWeight * Scale(avgRating, RatingLo, RatingHi)
        + ImpMotmWeight * Scale(motmRate, MotmRateLo, MotmRateHi);

    /// <param name="attendance">Fração (0..1) das partidas do clube no período em que o jogador jogou.</param>
    /// <param name="ratingStdDev">Desvio-padrão (populacional) das notas do jogador.</param>
    /// <param name="matches">Com 1 jogo não há desvio: a metade de estabilidade fica neutra (50).</param>
    public static double RawReg(double attendance, double ratingStdDev, int matches)
    {
        var stability = matches < 2
            ? NeutralScore
            : Scale(RatingStdDevCeiling - ratingStdDev, 0, RatingStdDevCeiling);
        return RegAttendanceWeight * Math.Clamp(attendance, 0, 1) * MaxScore + RegStabilityWeight * stability;
    }

    public static double RawGol(double savePct, double goalsConcededPerMatch) =>
        GolSaveWeight * Scale(savePct, SavePctLo, SavePctHi)
        + GolConcededWeight * Scale(ConcededCeiling - goalsConcededPerMatch, 0, ConcededCeiling);

    // ------------------------------------------------------------------ posição

    /// <summary>
    /// Grupo de posição a partir do texto da posição (a EA manda "forward", "midfielder", "defender", "goalkeeper";
    /// também aceita siglas — ST, CAM, CB, GK... — e termos em português). Desconhecida/vazia: MEIO.
    /// </summary>
    public static string PositionGroup(string? position)
    {
        var p = (position ?? "").Trim().ToLowerInvariant();
        if (p.Length == 0) return GroupMidfield;

        switch (p)
        {
            case "gk": case "gol": case "gl": case "goleiro":
                return GroupKeeper;
            case "cb": case "lb": case "rb": case "lwb": case "rwb": case "sw": case "ld": case "le": case "zag": case "def":
                return GroupDefense;
            case "st": case "cf": case "lw": case "rw": case "lf": case "rf": case "ss": case "fw": case "att": case "ata": case "pe": case "pd": case "ca":
                return GroupAttack;
            case "cm": case "cdm": case "cam": case "lm": case "rm": case "dm": case "am": case "mid": case "mc": case "vol": case "mei":
                return GroupMidfield;
        }

        if (p.Contains("goalkeep") || p.Contains("keeper") || p.Contains("goleir")) return GroupKeeper;
        // "wingback"/"fullback"/"centreback" contêm "wing"/"back": a defesa é testada antes do ataque.
        if (p.Contains("defen") || p.Contains("back") || p.Contains("zague") || p.Contains("later") || p.Contains("sweeper")) return GroupDefense;
        if (p.Contains("forward") || p.Contains("strik") || p.Contains("wing") || p.Contains("attack") || p.Contains("atac")
            || p.Contains("ponta") || p.Contains("centroav")) return GroupAttack;
        return GroupMidfield;
    }

    // ------------------------------------------------------------------ overall e faixa

    /// <summary>Pesos do overall por grupo (somam 1): ATA, CRI, PAS, DEF, IMP, REG, GOL.</summary>
    public static (double Ata, double Cri, double Pas, double Def, double Imp, double Reg, double Gol) Weights(string group) => group switch
    {
        GroupAttack => (0.35, 0.15, 0.10, 0.05, 0.25, 0.10, 0),
        GroupDefense => (0.05, 0.05, 0.15, 0.40, 0.20, 0.15, 0),
        GroupKeeper => (0, 0, 0.10, 0, 0.25, 0.15, 0.50),
        _ => (0.10, 0.25, 0.25, 0.15, 0.15, 0.10, 0) // MEIO e desconhecido
    };

    /// <summary>round(soma ponderada), limitado a 1..99. Eixos nulos pesam 0 (ATA de goleiro, GOL de linha).</summary>
    public static int Overall(string group, CardAxes axes)
    {
        var w = Weights(group);
        var sum = w.Ata * (axes.Ata ?? 0) + w.Cri * axes.Cri + w.Pas * axes.Pas + w.Def * axes.Def
                  + w.Imp * axes.Imp + w.Reg * axes.Reg + w.Gol * (axes.Gol ?? 0);
        return (int)Math.Clamp(Math.Round(sum, MidpointRounding.AwayFromZero), 1, MaxScore);
    }

    /// <summary>&lt; 60 bronze, 60–74 prata, 75–84 ouro, ≥ 85 elite.</summary>
    public static string Tier(int overall) =>
        overall >= EliteFrom ? TierElite : overall >= GoldFrom ? TierGold : overall >= SilverFrom ? TierSilver : TierBronze;
}
