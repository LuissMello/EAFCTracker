// TEST AID ONLY (see DemoDataSeeder.cs header): runs only with EAFCSettings:UseInMemoryDb=true AND Seed:Demo=true.
// Seeds ~10 weeks of "game nights" for the demo club so the analytics pages (Noite de jogo, Laboratório, Retrospectiva)
// have realistic data: skill-rating snapshots for both clubs, per-player stats, goal links, varying squad sizes,
// one night crossing midnight, one night with 7 matches, FC26 on the older nights and FC27 on the recent ones.
// It also feeds the "Cartas" page (player cards / comparator): shots, passes, tackles, saves and goals conceded per
// player, a second red card, club attributes (overall/height) for SOME players only, and very different sample sizes
// (Marcelo plays 8 matches = provisional card, Pedrinho only 2). The extra stats use their own Random so the lineups,
// goals and ratings of the other pages keep the same shape.
using System.Globalization;
using EAFCMatchTracker.Application.Services;
using EAFCMatchTracker.Domain.Entities;
using EAFCMatchTracker.Infrastructure.Data;

namespace EAFCMatchTracker.Api.Infrastructure.Dev;

internal static class DemoGameNights
{
    internal sealed record NightPlan(int DaysAgo, int StartHour, int StartMinute, bool Fc27, int[] Offsets, (int Opp, int Ours, int Theirs)[] Matches);

    // Offsets = minutes after the first match. Night 3 starts 22:00 local and ends 01:10 the next day.
    internal static readonly NightPlan[] Nights =
    {
        new(66, 21, 0, false, new[] { 0, 42, 84, 126 }, new[] { (0, 3, 1), (1, 2, 2), (2, 0, 1), (0, 4, 2) }),
        new(52, 20, 30, false, new[] { 0, 40, 85, 125, 170 }, new[] { (1, 1, 0), (2, 2, 3), (0, 2, 0), (1, 3, 3), (2, 1, 2) }),
        new(39, 22, 0, true, new[] { 0, 45, 95, 145, 190 }, new[] { (0, 2, 1), (1, 0, 1), (2, 1, 1), (0, 5, 2), (1, 3, 2) }),
        new(26, 21, 15, true, new[] { 0, 45, 90, 135 }, new[] { (2, 0, 2), (1, 1, 3), (0, 2, 2), (2, 1, 0) }),
        new(15, 19, 30, true, new[] { 0, 38, 76, 114, 152, 190, 228 }, new[] { (0, 4, 0), (1, 2, 1), (2, 3, 1), (0, 1, 0), (1, 2, 2), (2, 0, 1), (0, 3, 2) }),
        new(8, 21, 0, true, new[] { 0, 44, 88 }, new[] { (1, 2, 0), (2, 1, 1), (0, 2, 3) }),
        new(3, 20, 45, true, new[] { 0, 40, 82, 125 }, new[] { (0, 3, 0), (2, 2, 1), (1, 1, 2), (0, 4, 3) }),
    };

    private static readonly double[] PlayProbability = { 0.9, 0.8, 0.7, 0.6, 0.55, 0.6, 0.45, 0.4 }; // roster order (active 8)
    private static readonly int[] SquadSizes = { 5, 6, 4, 5, 5, 6, 4, 5, 6, 5, 4 };
    private static readonly int[] OpponentSkillGap = { 25, -45, 70 };
    private static readonly int[] OpponentDivision = { 4, 6, 3 };

    // Sample sizes for the cards page: these two only play in the listed matches (index k across all game nights).
    private static readonly HashSet<int> MarceloMatches = new() { 3, 8, 12, 15, 19, 23, 26, 30 }; // 8 -> provisional card
    private static readonly HashSet<int> PedrinhoMatches = new() { 24, 31 };                       // 2 matches
    private const int MarceloIdx = 6, PedrinhoIdx = 7, SicranoIdx = 2;

    // Club-member attributes (ProOverall / ProHeight): only some players have them, the page must cope with the rest.
    private static readonly Dictionary<string, (int Overall, int Height)> Attributes = new()
    {
        ["Beltrano"] = (84, 181), ["Fulano"] = (80, 176), ["Ciclano"] = (76, 188), ["Tiago"] = (78, 191),
    };

    // Per-player rating offset ("skill"), so the cards span several tiers instead of everybody sitting at the same level.
    private static readonly Dictionary<string, double> Skill = new()
    {
        ["Fulano"] = 1.1, ["Beltrano"] = 2.0, ["Tiago"] = 2.3, ["Sicrano"] = 1.7, ["Ciclano"] = 0.5, ["Zeca"] = 1.3,
        ["Pedrinho"] = 0.8, ["Marcelo"] = 0.0,
    };

    // Fixed ratings of the 4 inactive players in the old (FC26) history matches; the default would be a flat 7.0 for everybody.
    internal static readonly Dictionary<string, double> HistoryRating = new() { ["Rafa"] = 7.8, ["Veterano"] = 7.2, ["Lucas"] = 7.0, ["Jonas"] = 6.6 };

    private sealed record Line(PlayerEntity Player, string Pos, int Goals, int Assists, int Pre, double Rating, bool Mom, int Reds);

    public static async Task<int> SeedAsync(
        EAFCContext db, List<PlayerEntity> players, List<PlayerMatchStatsEntity> stats,
        int? currentVersionId, int? oldVersionId, DateTime now)
    {
        var rnd = new Random(2026);
        var statRnd = new Random(31337); // independent stream: card stats must not shift the lineups/goals of the other pages
        var zone = ClubSessionService.ResolveZone("America/Sao_Paulo");
        var active = players.Take(PlayProbability.Length).ToList();

        // Opponent squads (6 players each) so the opponent's player count can vary per match.
        var oppSquads = new List<List<PlayerEntity>>();
        for (var o = 0; o < DemoDataSeeder.Opponents.Length; o++)
        {
            var squad = Enumerable.Range(0, 6).Select(j => new PlayerEntity
            {
                PlayerId = 8100 + o * 10 + j,
                ClubId = DemoDataSeeder.Opponents[o].Id,
                Playername = $"{DemoDataSeeder.Opponents[o].Name.Split(' ')[0]}-{j + 1}"
            }).ToList();
            db.Players.AddRange(squad);
            oppSquads.Add(squad);
        }
        await db.SaveChangesAsync();
        var oppStats = oppSquads.SelectMany(s => s).Select(p => new PlayerMatchStatsEntity { PlayerEntityId = p.Id }).ToList();
        db.PlayerMatchStats.AddRange(oppStats);
        await db.SaveChangesAsync();
        foreach (var p in oppSquads.SelectMany(s => s)) p.PlayerMatchStatsId = oppStats.First(s => s.PlayerEntityId == p.Id).Id;
        await db.SaveChangesAsync();
        var statsByPlayer = stats.Concat(oppStats).ToDictionary(s => s.PlayerEntityId);

        var sr = 1450;
        var k = 0;
        for (var n = 0; n < Nights.Length; n++)
        {
            var plan = Nights[n];
            var localDay = TimeZoneInfo.ConvertTimeFromUtc(now, zone).Date.AddDays(-plan.DaysAgo);
            var versionId = plan.Fc27 ? currentVersionId : oldVersionId;

            for (var m = 0; m < plan.Matches.Length; m++, k++)
            {
                var (oppIdx, ours, theirs) = plan.Matches[m];
                var opp = DemoDataSeeder.Opponents[oppIdx];
                var local = DateTime.SpecifyKind(localDay.AddHours(plan.StartHour).AddMinutes(plan.StartMinute + plan.Offsets[m]), DateTimeKind.Unspecified);
                var ts = DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeToUtc(local, zone), DateTimeKind.Utc);
                var matchId = 8_100_000_000L + k;

                var hatTrick = n == 6 && m == 0;       // 3-0 (FC27): all three goals by the same forward
                var redCard = n == 3 && m == 1;        // the 1-3 defeat: a defender is sent off
                var redCard2 = k == 20;                // a forward is sent off too (cards page: "red cards" varies)
                var forced = new List<int>();
                if (hatTrick) forced.Add(0);
                if (redCard) forced.Add(MarceloIdx);
                if (redCard2) forced.Add(SicranoIdx);
                if (MarceloMatches.Contains(k) && !forced.Contains(MarceloIdx)) forced.Add(MarceloIdx);
                if (PedrinhoMatches.Contains(k)) forced.Add(PedrinhoIdx);
                var banned = new HashSet<int>();
                if (!MarceloMatches.Contains(k)) banned.Add(MarceloIdx);
                if (!PedrinhoMatches.Contains(k)) banned.Add(PedrinhoIdx);

                var lineup = PickLineup(active, SquadSizes[k % SquadSizes.Length], forced, banned, rnd);
                var goalPlan = PlanGoals(lineup, ours, hatTrick ? lineup.First(p => p.Playername == "Beltrano") : null, rnd);

                var skipLinks = (n == 1 && m == 0) || (n == 5 && m == 2); // two matches intentionally left without goal links
                var lines = lineup.Select(p =>
                {
                    var g = goalPlan.Count(x => x.Scorer == p);
                    var a = goalPlan.Count(x => x.Assist == p);
                    var pre = skipLinks ? 0 : goalPlan.Count(x => x.Pre == p); // sem links, PreAssists = 0 (como no app real)
                    var reds = (redCard && p.Playername == "Marcelo") || (redCard2 && p.Playername == "Sicrano") ? 1 : 0;
                    var rating = 6.0 + 0.55 * g + 0.3 * a + (ours > theirs ? 0.4 : ours < theirs ? -0.4 : 0) - 1.0 * reds + (rnd.NextDouble() - 0.5) * 1.2 + Skill.GetValueOrDefault(p.Playername ?? "");
                    return new Line(p, PosFor(p.Playername, k), g, a, pre, Math.Round(Math.Clamp(rating, 4.5, 9.8), 1), false, reds);
                }).ToList();
                var best = lines.OrderByDescending(l => l.Rating).ThenByDescending(l => l.Goals).First();
                lines = lines.Select(l => l == best ? l with { Mom = true } : l).ToList();

                // Result -> SR after the match (our SR drifts up with a dip in night 4).
                sr += ours > theirs ? 11 : ours < theirs ? -13 : 1;
                var oppSr = Math.Max(1000, sr + OpponentSkillGap[oppIdx] + rnd.Next(-20, 21));
                var ourDivision = n >= 2 ? 4 : 5;

                db.Matches.Add(new MatchEntity { MatchId = matchId, Timestamp = ts, MatchType = MatchType.League, GameVersionId = versionId });
                db.MatchClubs.Add(DemoMatch.BuildClub(matchId, ts, DemoDataSeeder.OurClubId, "Nosso Clube (demo)", ours, theirs, DemoDataSeeder.OurClubId, ourDivision));
                db.MatchClubs.Add(DemoMatch.BuildClub(matchId, ts, opp.Id, opp.Name, theirs, ours, DemoDataSeeder.OurClubId, OpponentDivision[oppIdx]));

                foreach (var l in lines)
                {
                    var row = Row(matchId, DemoDataSeeder.OurClubId, l.Player, statsByPlayer, l.Pos, l.Goals, l.Assists, l.Pre, l.Rating, l.Mom, l.Reds);
                    AddCardStats(row, l.Player.Playername, ours, theirs, statRnd);
                    db.MatchPlayers.Add(row);
                }

                var oppLineup = oppSquads[oppIdx].OrderBy(_ => rnd.Next()).Take(3 + rnd.Next(4)).ToList();
                var oppGoals = oppLineup.Select(_ => 0).ToArray();
                for (var g = 0; g < theirs; g++) oppGoals[rnd.Next(oppLineup.Count)]++;
                for (var i = 0; i < oppLineup.Count; i++)
                    db.MatchPlayers.Add(Row(matchId, opp.Id, oppLineup[i], statsByPlayer, "midfielder", oppGoals[i], 0, 0,
                        Math.Round(5.5 + rnd.NextDouble() * 2.5 + oppGoals[i] * 0.4, 1), false, 0));

                // Goal links for most matches (two matches intentionally left without links).
                if (!skipLinks)
                    foreach (var g in goalPlan)
                        db.MatchGoalLinks.Add(new MatchGoalLinkEntity
                        {
                            MatchId = matchId, ClubId = DemoDataSeeder.OurClubId, ScorerPlayerEntityId = g.Scorer.Id,
                            AssistPlayerEntityId = g.Assist?.Id, PreAssistPlayerEntityId = g.Pre?.Id
                        });

                db.OverallStats.Add(Overall(DemoDataSeeder.OurClubId, matchId, sr, ourDivision, n >= 2 ? "3" : "2", versionId, ts));
                db.OverallStats.Add(Overall(opp.Id, matchId, oppSr, OpponentDivision[oppIdx], "1", versionId, ts));
            }
        }

        await db.SaveChangesAsync();
        return k;
    }

    private static OverallStatsEntity Overall(long clubId, long matchId, int sr, int division, string promotions, int? versionId, DateTime ts) => new()
    {
        ClubId = clubId, MatchId = matchId, SkillRating = sr.ToString(CultureInfo.InvariantCulture), CurrentDivision = division,
        Promotions = promotions, Relegations = "1", GameVersionId = versionId, UpdatedAtUtc = ts
    };

    private static MatchPlayerEntity Row(
        long matchId, long clubId, PlayerEntity p, Dictionary<long, PlayerMatchStatsEntity> stats, string pos,
        int goals, int assists, int pre, double rating, bool mom, int reds) => new()
    {
        MatchId = matchId, ClubId = clubId, PlayerEntityId = p.Id, PlayerMatchStatsEntityId = stats[p.Id].Id,
        Goals = (short)goals, Assists = (short)assists, PreAssists = (short)pre, Rating = rating, Mom = mom, Redcards = (short)reds,
        Pos = pos, ProName = p.Playername, SecondsPlayed = 5400, Score = (short)goals,
        Realtimegame = "", Realtimeidle = "", Vproattr = "", Vprohackreason = "",
        MatchEventAggregate0 = "", MatchEventAggregate1 = "", MatchEventAggregate2 = "", MatchEventAggregate3 = ""
    };

    /// <summary>
    /// Shots/passes/tackles by position, saves + goals conceded for the keeper, clean sheets, W/L and club attributes
    /// (some players only). Uses its own Random so everything else in the seed stays as it was.
    /// </summary>
    internal static void AddCardStats(MatchPlayerEntity row, string? name, int ours, int theirs, Random r)
    {
        var pos = row.Pos;
        var boost = Math.Clamp(Skill.GetValueOrDefault(name ?? "") * 0.05, 0, 0.09); // better players are a bit more accurate
        (int Lo, int Hi) shotsExtra, passAtt, tackleAtt;
        (double Lo, double Hi) passAcc, tackleAcc;
        switch (pos)
        {
            case "forward": shotsExtra = (0, boost >= 0.055 ? 1 : 3); passAtt = (12, 24); passAcc = (0.62, 0.76); tackleAtt = (1, 4); tackleAcc = (0.20, 0.60); break;
            case "defender": shotsExtra = (0, 1); passAtt = (20, 36); passAcc = (0.68, 0.82); tackleAtt = (4, 8); tackleAcc = (0.35, 0.75); break;
            case "goalkeeper": shotsExtra = (0, 0); passAtt = (8, 18); passAcc = (0.50, 0.70); tackleAtt = (0, 0); tackleAcc = (0, 0); break;
            default: shotsExtra = (0, 2); passAtt = (22, 40); passAcc = (0.62, 0.78); tackleAtt = (2, 6); tackleAcc = (0.25, 0.65); break;
        }

        row.Shots = (short)(row.Goals + r.Next(shotsExtra.Lo, shotsExtra.Hi + 1));
        row.Passattempts = (short)r.Next(passAtt.Lo, passAtt.Hi + 1);
        row.Passesmade = (short)Math.Round(row.Passattempts * Math.Min(0.92, boost + passAcc.Lo + r.NextDouble() * (passAcc.Hi - passAcc.Lo)));
        row.Tackleattempts = (short)r.Next(tackleAtt.Lo, tackleAtt.Hi + 1);
        row.Tacklesmade = (short)Math.Round(row.Tackleattempts * Math.Min(0.95, boost + tackleAcc.Lo + r.NextDouble() * (tackleAcc.Hi - tackleAcc.Lo)));

        row.Goalsconceded = (short)theirs;
        row.Wins = (short)(ours > theirs ? 1 : 0);
        row.Losses = (short)(ours < theirs ? 1 : 0);
        if (theirs == 0)
        {
            row.Cleansheetsany = 1;
            if (pos == "goalkeeper") row.Cleansheetsgk = 1;
            if (pos == "defender") row.Cleansheetsdef = 1;
        }
        if (pos == "goalkeeper") row.Saves = (short)(r.Next(2, 7) + (boost > 0.03 ? 3 : 0)); // shots on target faced = saves + goals conceded

        // Arquétipo por posição (mapa do jogo: GK 1/2, DEF 3-6, MEI 7-10, ATA 11-13), determinístico (sem Random: não mexe nas outras páginas).
        var ordinal = row.MatchId >= 8_100_000_000L ? row.MatchId - 8_100_000_000L : -1 - (row.MatchId - 8_000_000_000L);
        row.Archetypeid = ArchetypeFor(name, ordinal, pos);

        if (name is not null && Attributes.TryGetValue(name, out var attr))
        {
            // o overall muda com o arquétipo (quem troca de arquétipo troca de nível): -2 nos arquétipos alternativos
            var altOverall = (name == "Beltrano" && row.Archetypeid == 11) || (name == "Tiago" && row.Archetypeid == 2) ? -2 : 0;
            attr = (attr.Overall + altOverall, attr.Height);
            row.ProOverall = attr.Overall;
            row.ProOverallStr = attr.Overall.ToString(CultureInfo.InvariantCulture);
            row.ProHeight = attr.Height;
        }
    }

    /// <summary>Posição do jogador na partida k das noites: igual à do elenco, exceto a última da noite 3 (Zeca joga de zagueiro).</summary>
    private static string PosFor(string? name, int k) =>
        name == "Zeca" && k == 13 ? "defender" : DemoDataSeeder.PosOf(name);

    /// <summary>
    /// Arquétipo (id da EA) de cada jogador na partida de ordem <paramref name="ordinal"/> (0.. nas noites; negativo = histórico antigo).
    /// Alguns trocam ao longo do tempo (Beltrano 12→11, Tiago 1→2, Zeca 7→9, Marcelo 3→5), Pedrinho alterna 9/10, uma em cada ~11
    /// partidas vem sem dado (0, como as partidas antigas reais) e há um id isolado fora do catálogo (30) para testar rótulos padrão.
    /// </summary>
    internal static short ArchetypeFor(string? name, long ordinal, string? pos = null)
    {
        if (name == "Zeca" && pos == "defender") return 6; // jogou de zagueiro nessa partida: arquétipo de defesa
        if (Math.Abs(ordinal) % 11 == 3) return 0;
        if (ordinal is >= 5 and <= 12 && name == "Ciclano") return 30;
        return (short)(name switch
        {
            "Tiago" => ordinal < 18 ? 1 : 2,
            "Lucas" => 2,
            "Ciclano" => 4,
            "Marcelo" => ordinal < 20 ? 3 : 5,
            "Veterano" => 6,
            "Fulano" => 8,
            "Zeca" => ordinal < 11 ? 7 : 9, // troca DENTRO da noite 3 (partidas 9-13)
            "Pedrinho" => ordinal % 2 == 0 ? 10 : 9,
            "Rafa" => 7,
            "Beltrano" => ordinal < 22 ? 12 : 11,
            "Sicrano" => 13,
            "Jonas" => 11,
            _ => 8
        });
    }

    /// <summary>Weighted sampling without replacement (Efraimidis-Spirakis) by each player's play probability.</summary>
    private static List<PlayerEntity> PickLineup(List<PlayerEntity> active, int size, List<int> forced, HashSet<int> banned, Random rnd)
    {
        var chosen = forced.Select(i => active[i]).ToList();
        var rest = active.Select((p, i) => (Player: p, Index: i, Key: Math.Pow(rnd.NextDouble(), 1.0 / PlayProbability[i])))
            .Where(x => !chosen.Contains(x.Player) && !banned.Contains(x.Index))
            .OrderByDescending(x => x.Key).Select(x => x.Player);
        chosen.AddRange(rest.Take(Math.Max(0, size - chosen.Count)));
        return chosen;
    }

    private sealed record PlannedGoal(PlayerEntity Scorer, PlayerEntity? Assist, PlayerEntity? Pre);

    // The "stars" of the cards page: Beltrano is the top scorer, Fulano the playmaker (the rest follow the position weights).
    private static readonly Dictionary<string, double> ScoreBoost = new() { ["Beltrano"] = 9, ["Sicrano"] = 4, ["Pedrinho"] = 3, ["Fulano"] = 2.5 };
    private static readonly Dictionary<string, double> AssistBoost = new() { ["Fulano"] = 10, ["Pedrinho"] = 4, ["Beltrano"] = 7, ["Zeca"] = 5 };

    private static double ScoreWeight(PlayerEntity p) =>
        ScoreBoost.TryGetValue(p.Playername ?? "", out var w) ? w
        : DemoDataSeeder.PosOf(p.Playername) switch { "forward" => 3, "midfielder" => 2, "defender" => 0.6, _ => 0.05 };

    private static double AssistWeight(PlayerEntity p) =>
        AssistBoost.TryGetValue(p.Playername ?? "", out var w) ? w
        : DemoDataSeeder.PosOf(p.Playername) switch { "midfielder" => 3, "forward" => 2, "defender" => 1, _ => 0.1 };

    private static PlayerEntity Pick(IEnumerable<PlayerEntity> candidates, Func<PlayerEntity, double> weight, Random rnd)
    {
        var list = candidates.ToList();
        var total = list.Sum(weight);
        var r = rnd.NextDouble() * total;
        foreach (var p in list)
        {
            r -= weight(p);
            if (r <= 0) return p;
        }
        return list[^1];
    }

    /// <summary>Scorer/assist/pre-assist per goal. Nobody scores 3+ (no accidental hat-tricks) unless <paramref name="hatTrickBy"/>.</summary>
    private static List<PlannedGoal> PlanGoals(List<PlayerEntity> lineup, int goals, PlayerEntity? hatTrickBy, Random rnd)
    {
        var plan = new List<PlannedGoal>();
        for (var g = 0; g < goals; g++)
        {
            var scorer = hatTrickBy ?? Pick(lineup.Where(p => plan.Count(x => x.Scorer == p) < 2), ScoreWeight, rnd);
            PlayerEntity? assist = null, pre = null;
            if (rnd.NextDouble() < 0.78)
            {
                assist = Pick(lineup.Where(p => p != scorer), AssistWeight, rnd);
                if (rnd.NextDouble() < 0.4 && lineup.Count > 2)
                    pre = Pick(lineup.Where(p => p != scorer && p != assist), AssistWeight, rnd);
            }
            plan.Add(new PlannedGoal(scorer, assist, pre));
        }
        return plan;
    }
}
