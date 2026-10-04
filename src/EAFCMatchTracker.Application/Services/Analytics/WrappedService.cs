using System.Globalization;
using EAFCMatchTracker.Application.Dtos;
using EAFCMatchTracker.Application.Interfaces.Services;
using EAFCMatchTracker.Infrastructure.Data;
using Microsoft.Extensions.Caching.Memory;

namespace EAFCMatchTracker.Application.Services.Analytics;

/// <summary>"Retrospectiva" (wrapped) do clube por edição do jogo. Somente leitura.</summary>
public sealed class WrappedService : IWrappedService
{
    public const int MaxSrPoints = 120;
    public const int MinSessionMatches = 3;
    public const int MinOpponentMatches = 3;
    public const int MinRatingMatches = 10;
    public const int MinBestMonthMatches = 5;
    public const int MinBestDuoGoals = 2;
    public const int SessionOverheadMinutes = 15;

    private static readonly CultureInfo Pt = CultureInfo.GetCultureInfo("pt-BR");

    private static readonly string[] WeekdayNames =
        { "domingo", "segunda-feira", "terça-feira", "quarta-feira", "quinta-feira", "sexta-feira", "sábado" };

    private readonly ClubSessionService _sessions;
    private readonly IMemoryCache _cache;
    private readonly ClubAnalyticsLoader _loader;

    public WrappedService(EAFCContext db, ClubSessionService sessions, IMemoryCache cache)
    {
        _sessions = sessions;
        _cache = cache;
        _loader = new ClubAnalyticsLoader(db);
    }

    public Task<WrappedDto> GetWrappedAsync(long clubId, int? gameVersion, CancellationToken ct) =>
        AnalyticsCache.GetOrCreateAsync(_cache, _loader, "wrapped", clubId, $"v={gameVersion}", async () =>
        {
            var info = await _loader.GetClubInfoAsync(clubId, ct);
            var (known, versionId, versionName) = await _loader.ResolveVersionAsync(gameVersion, ct);
            var data = known
                ? await _loader.LoadAsync(clubId, versionId, null, null, null, DataParts.All, ct)
                : new ClubDataset();
            var sessions = data.Matches.Count > 0 ? await _sessions.GetForClubAsync(clubId, ct) : new List<ClubSession>();
            var dto = Build(clubId, info, data, sessions);
            dto.GameVersion = gameVersion;
            dto.GameVersionName = versionName;
            return dto;
        }, ct);

    private sealed record SessionSlice(ClubSession Session, List<MatchRow> Matches);

    internal static WrappedDto Build(long clubId, ClubInfo info, ClubDataset data, List<ClubSession> allSessions)
    {
        var rows = data.Matches;
        var zone = info.Zone;
        var dto = new WrappedDto { ClubId = clubId, ClubName = info.Name, TimeZoneId = info.TimeZoneId };
        if (rows.Count == 0) return dto;

        var byId = rows.ToDictionary(m => m.MatchId);
        var slices = new List<SessionSlice>();
        foreach (var s in allSessions)
        {
            var ms = s.MatchIds.Where(byId.ContainsKey).Select(id => byId[id]).ToList();
            if (ms.Count > 0) slices.Add(new SessionSlice(s, ms));
        }

        DateOnly Local(MatchRow m) => StatsUtil.LocalDate(m.Timestamp, zone);

        dto.From = Local(rows[0]);
        dto.To = Local(rows[^1]);
        dto.Totals = BuildTotals(rows, slices, zone);
        dto.Streaks = new WrappedStreaksDto
        {
            LongestWin = Longest(rows, m => m.Gf > m.Ga, Local),
            LongestUnbeaten = Longest(rows, m => m.Gf >= m.Ga, Local),
            LongestWinless = Longest(rows, m => m.Gf <= m.Ga, Local),
            LongestCleanSheet = Longest(rows, m => m.Ga == 0, Local)
        };
        dto.BigMoments = BuildMoments(rows, slices, zone);
        var (players, hatTricks) = BuildPlayers(data);
        dto.Players = players;
        dto.BestDuo = BuildBestDuo(data);
        dto.Opponents = BuildOpponents(rows);
        dto.Rhythm = BuildRhythm(rows, zone);
        dto.Progression = BuildProgression(rows, zone);
        dto.FunFacts = BuildFunFacts(dto);
        _ = hatTricks;
        return dto;
    }

    private static WrappedTotalsDto BuildTotals(List<MatchRow> rows, List<SessionSlice> slices, TimeZoneInfo zone)
    {
        var wins = rows.Count(m => m.Gf > m.Ga);
        var losses = rows.Count(m => m.Gf < m.Ga);
        var minutes = slices.Sum(s => (s.Matches[^1].Timestamp - s.Matches[0].Timestamp).TotalMinutes)
                      + slices.Count * SessionOverheadMinutes;
        return new WrappedTotalsDto
        {
            Matches = rows.Count,
            Wins = wins,
            Draws = rows.Count - wins - losses,
            Losses = losses,
            WinRatePct = StatsUtil.Pct(wins, rows.Count),
            GoalsFor = rows.Sum(m => m.Gf),
            GoalsAgainst = rows.Sum(m => m.Ga),
            CleanSheets = rows.Count(m => m.Ga == 0),
            Sessions = slices.Count,
            ActiveDays = rows.Select(m => StatsUtil.LocalDate(m.Timestamp, zone)).Distinct().Count(),
            EstimatedMinutes = (int)Math.Round(minutes)
        };
    }

    /// <summary>Maior sequência consecutiva (em ordem cronológica) que satisfaz o predicado; empate fica com a mais antiga.</summary>
    internal static WrappedStreakDto? Longest(List<MatchRow> rows, Func<MatchRow, bool> predicate, Func<MatchRow, DateOnly> localDate)
    {
        int bestLen = 0, bestStart = 0, runStart = 0, runLen = 0;
        for (var i = 0; i < rows.Count; i++)
        {
            if (predicate(rows[i]))
            {
                if (runLen == 0) runStart = i;
                runLen++;
                if (runLen > bestLen) { bestLen = runLen; bestStart = runStart; }
            }
            else runLen = 0;
        }
        return bestLen == 0
            ? null
            : new WrappedStreakDto { Length = bestLen, From = localDate(rows[bestStart]), To = localDate(rows[bestStart + bestLen - 1]) };
    }

    private static WrappedBigMomentsDto BuildMoments(List<MatchRow> rows, List<SessionSlice> slices, TimeZoneInfo zone)
    {
        var biggestWin = StatsUtil.BiggestWin(rows);
        var worstLoss = StatsUtil.WorstLoss(rows);
        var highest = rows
            .OrderByDescending(m => m.Gf + m.Ga).ThenByDescending(m => Math.Abs(m.Gf - m.Ga)).ThenBy(m => m.Timestamp).ThenBy(m => m.MatchId)
            .First();

        var eligible = slices.Where(s => s.Matches.Count >= MinSessionMatches)
            .Select(s => (Slice: s, Ppm: s.Matches.Sum(m => m.Points) / (double)s.Matches.Count))
            .ToList();
        var best = eligible
            .OrderByDescending(x => x.Ppm).ThenByDescending(x => x.Slice.Matches.Count).ThenByDescending(x => x.Slice.Matches[0].Timestamp)
            .Cast<(SessionSlice Slice, double Ppm)?>().FirstOrDefault();
        var worst = eligible
            .OrderBy(x => x.Ppm).ThenByDescending(x => x.Slice.Matches.Count).ThenByDescending(x => x.Slice.Matches[0].Timestamp)
            .Cast<(SessionSlice Slice, double Ppm)?>().FirstOrDefault();
        // Com uma única noite elegível (ou todas iguais) não há "pior": evita repetir a mesma noite nos dois cards.
        if (best is not null && worst is not null && (best.Value.Slice.Session.Id == worst.Value.Slice.Session.Id || Math.Abs(best.Value.Ppm - worst.Value.Ppm) < 1e-9))
            worst = null;

        return new WrappedBigMomentsDto
        {
            BiggestWin = biggestWin is null ? null : StatsUtil.MatchRef(biggestWin),
            WorstLoss = worstLoss is null ? null : StatsUtil.MatchRef(worstLoss),
            HighestScoring = StatsUtil.MatchRef(highest),
            BestSession = best is null ? null : SessionRef(best.Value.Slice),
            WorstSession = worst is null ? null : SessionRef(worst.Value.Slice)
        };
    }

    private static WrappedSessionRefDto SessionRef(SessionSlice s) => new()
    {
        SessionId = s.Session.Id,
        Date = s.Session.Date,
        Wins = s.Matches.Count(m => m.Gf > m.Ga),
        Draws = s.Matches.Count(m => m.Gf == m.Ga),
        Losses = s.Matches.Count(m => m.Gf < m.Ga)
    };

    private sealed class Agg
    {
        public long Id { get; init; }
        public string Name { get; init; } = "";
        public int Matches { get; set; }
        public int Rated { get; set; }
        public double RatingSum { get; set; }
        public int Goals { get; set; }
        public int Assists { get; set; }
        public int Motm { get; set; }
        public int Reds { get; set; }
    }

    private static (WrappedPlayersDto Players, int HatTricks) BuildPlayers(ClubDataset data)
    {
        var aggs = data.PlayersByMatch.Values.SelectMany(l => l).GroupBy(p => p.PlayerId)
            .Select(g => new Agg
            {
                Id = g.Key,
                Name = data.NameOf(g.Key),
                Matches = g.Count(),
                Rated = g.Count(r => r.Rating > 0),
                RatingSum = g.Where(r => r.Rating > 0).Sum(r => r.Rating),
                Goals = g.Sum(r => r.Goals),
                Assists = g.Sum(r => r.Assists),
                Motm = g.Count(r => r.Mom),
                Reds = g.Sum(r => r.RedCards)
            }).ToList();

        WrappedPlayerStatDto? Pick(IEnumerable<Agg> source, Func<Agg, double> value) => source
            .OrderByDescending(value).ThenBy(a => a.Matches).ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .Select(a => new WrappedPlayerStatDto { PlayerEntityId = a.Id, Name = a.Name, Value = StatsUtil.Round2(value(a)), Matches = a.Matches })
            .FirstOrDefault();

        var mostMatches = aggs
            .OrderByDescending(a => a.Matches).ThenByDescending(a => a.Goals).ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .Select(a => new WrappedPlayerStatDto { PlayerEntityId = a.Id, Name = a.Name, Value = a.Matches, Matches = a.Matches })
            .FirstOrDefault();

        var hatTricks = data.PlayersByMatch.Values.SelectMany(l => l).Count(r => r.Goals >= 3);
        var players = new WrappedPlayersDto
        {
            TopScorer = Pick(aggs.Where(a => a.Goals > 0), a => a.Goals),
            TopAssister = Pick(aggs.Where(a => a.Assists > 0), a => a.Assists),
            MostMotm = Pick(aggs.Where(a => a.Motm > 0), a => a.Motm),
            MostRedCards = Pick(aggs.Where(a => a.Reds > 0), a => a.Reds),
            BestAvgRating = aggs.Where(a => a.Rated >= MinRatingMatches)
                .OrderByDescending(a => a.RatingSum / a.Rated).ThenByDescending(a => a.Rated).ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                .Select(a => new WrappedPlayerStatDto { PlayerEntityId = a.Id, Name = a.Name, Value = StatsUtil.Round2(a.RatingSum / a.Rated), Matches = a.Rated })
                .FirstOrDefault(),
            MostMatches = mostMatches,
            HatTricks = hatTricks
        };
        return (players, hatTricks);
    }

    /// <summary>Par assistência→gol mais frequente nos vínculos de gols (mínimo de 2 gols; autoassistência ignorada).</summary>
    private static WrappedBestDuoDto? BuildBestDuo(ClubDataset data)
    {
        var best = data.Goals
            .Where(g => g.AssistId.HasValue && g.AssistId.Value != g.ScorerId)
            .GroupBy(g => (Assist: g.AssistId!.Value, Scorer: g.ScorerId))
            .Select(g => (g.Key.Assist, g.Key.Scorer, Count: g.Count()))
            .Where(x => x.Count >= MinBestDuoGoals)
            .OrderByDescending(x => x.Count).ThenBy(x => x.Assist).ThenBy(x => x.Scorer)
            .Cast<(long Assist, long Scorer, int Count)?>()
            .FirstOrDefault();
        return best is null
            ? null
            : new WrappedBestDuoDto { ScorerName = data.NameOf(best.Value.Scorer), AssisterName = data.NameOf(best.Value.Assist), Goals = best.Value.Count };
    }

    private static WrappedOpponentsDto BuildOpponents(List<MatchRow> rows)
    {
        var records = StatsUtil.OpponentRecords(rows.Where(m => m.OppClubId != 0)).ToList();
        var eligible = records.Where(r => r.Matches >= MinOpponentMatches).ToList();
        return new WrappedOpponentsDto
        {
            MostFaced = records.OrderByDescending(r => r.Matches).ThenByDescending(r => r.Wins).ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase).FirstOrDefault(),
            // Vítima: venceram mais do que perderam; carrasco: o contrário (evita o mesmo rival nas duas pontas).
            FavoriteVictim = eligible.Where(r => r.Wins > r.Losses)
                .OrderByDescending(r => r.Wins / (double)r.Matches).ThenByDescending(r => r.Matches).ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(),
            Nemesis = eligible.Where(r => r.Losses > r.Wins)
                .OrderByDescending(r => r.Losses / (double)r.Matches).ThenByDescending(r => r.Matches).ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault()
        };
    }

    private static WrappedRhythmDto BuildRhythm(List<MatchRow> rows, TimeZoneInfo zone)
    {
        var local = rows.Select(m => (Row: m, Local: StatsUtil.ToLocal(m.Timestamp, zone))).ToList();

        var weekday = local.GroupBy(x => (int)x.Local.DayOfWeek)
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key).FirstOrDefault();
        var hour = local.GroupBy(x => x.Local.Hour)
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key).FirstOrDefault();

        var months = local.GroupBy(x => x.Local.ToString("yyyy-MM", CultureInfo.InvariantCulture))
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g =>
            {
                var r = g.Select(x => x.Row).ToList();
                return new WrappedMonthDto
                {
                    Month = g.Key,
                    Matches = r.Count,
                    Wins = r.Count(m => m.Gf > m.Ga),
                    Draws = r.Count(m => m.Gf == m.Ga),
                    Losses = r.Count(m => m.Gf < m.Ga)
                };
            }).ToList();

        var bestMonth = months.Where(m => m.Matches >= MinBestMonthMatches)
            .OrderByDescending(m => m.Wins / (double)m.Matches).ThenByDescending(m => m.Matches).ThenByDescending(m => m.Month, StringComparer.Ordinal)
            .FirstOrDefault();

        return new WrappedRhythmDto
        {
            BusiestWeekday = weekday is null ? null : new WrappedWeekdayDto { Weekday = weekday.Key, Matches = weekday.Count() },
            BusiestHour = hour is null ? null : new WrappedHourDto { Hour = hour.Key, Matches = hour.Count() },
            BestMonth = bestMonth is null ? null : new WrappedBestMonthDto
            {
                Month = bestMonth.Month,
                Matches = bestMonth.Matches,
                WinRatePct = StatsUtil.Pct(bestMonth.Wins, bestMonth.Matches)
            },
            Monthly = months
        };
    }

    private static WrappedProgressionDto BuildProgression(List<MatchRow> rows, TimeZoneInfo zone)
    {
        var points = rows.Where(m => m.OurSr.HasValue)
            .Select(m => new WrappedSrPointDto { Date = StatsUtil.LocalDate(m.Timestamp, zone), Value = m.OurSr!.Value })
            .ToList();

        var sr = new WrappedSkillRatingDto();
        var peakIdx = -1;
        var lowIdx = -1;
        if (points.Count > 0)
        {
            peakIdx = 0;
            lowIdx = 0;
            for (var i = 1; i < points.Count; i++)
            {
                if (points[i].Value > points[peakIdx].Value) peakIdx = i;
                if (points[i].Value < points[lowIdx].Value) lowIdx = i;
            }
            sr.Start = points[0].Value;
            sr.End = points[^1].Value;
            sr.Peak = points[peakIdx];
            sr.Low = points[lowIdx];
        }

        var divisions = rows.Where(m => m.OurDivision.HasValue).Select(m => m.OurDivision!.Value).ToList();
        var (promotions, relegations) = CountMovements(rows, divisions);

        return new WrappedProgressionDto
        {
            SkillRating = sr,
            Division = new WrappedDivisionDto
            {
                Start = divisions.Count > 0 ? divisions[0] : null,
                End = divisions.Count > 0 ? divisions[^1] : null,
                Promotions = promotions,
                Relegations = relegations
            },
            SrSeries = Downsample(points, peakIdx, lowIdx, MaxSrPoints)
        };
    }

    /// <summary>
    /// Promoções/rebaixamentos: soma dos aumentos entre snapshots consecutivos dos contadores acumulados da EA
    /// (aumentos negativos = reinício de temporada, ignorados). Sem contadores, conta mudanças de divisão
    /// (número menor = divisão melhor).
    /// </summary>
    internal static (int Promotions, int Relegations) CountMovements(List<MatchRow> rows, List<int> divisions)
    {
        static int Increments(IEnumerable<int?> values)
        {
            var list = values.Where(v => v.HasValue).Select(v => v!.Value).ToList();
            var sum = 0;
            for (var i = 1; i < list.Count; i++) sum += Math.Max(0, list[i] - list[i - 1]);
            return sum;
        }

        if (rows.Any(m => m.OurPromotions.HasValue || m.OurRelegations.HasValue))
            return (Increments(rows.Select(m => m.OurPromotions)), Increments(rows.Select(m => m.OurRelegations)));

        int up = 0, down = 0;
        for (var i = 1; i < divisions.Count; i++)
        {
            if (divisions[i] < divisions[i - 1]) up++;
            else if (divisions[i] > divisions[i - 1]) down++;
        }
        return (up, down);
    }

    /// <summary>Reduz a série a no máximo <paramref name="max"/> pontos, mantendo primeiro, último, pico e vale.</summary>
    internal static List<WrappedSrPointDto> Downsample(List<WrappedSrPointDto> points, int peakIdx, int lowIdx, int max)
    {
        if (points.Count <= max) return points;
        var keep = new SortedSet<int> { 0, points.Count - 1 };
        if (peakIdx >= 0) keep.Add(peakIdx);
        if (lowIdx >= 0) keep.Add(lowIdx);
        var slots = max - keep.Count;
        for (var k = 1; k <= slots; k++)
            keep.Add((int)Math.Round(k * (points.Count - 1.0) / (slots + 1)));
        // Índices arredondados podem repetir os essenciais (o conjunto ignora duplicatas): nunca passa de max.
        return keep.Take(max).Select(i => points[i]).ToList();
    }

    private static string Plural(int n, string singular, string plural) => n == 1 ? singular : plural;

    private static List<string> BuildFunFacts(WrappedDto w)
    {
        var facts = new List<string>();
        var t = w.Totals;
        if (t.Matches == 0) return facts;

        var hours = t.EstimatedMinutes / 60.0;
        var timeText = hours >= 1
            ? $"{(Math.Abs(hours - Math.Round(hours)) < 0.05 ? Math.Round(hours).ToString("0", Pt) : hours.ToString("0.0", Pt))} {Plural(hours >= 1.95 ? 2 : 1, "hora", "horas")}"
            : $"{t.EstimatedMinutes} minutos";
        facts.Add($"Vocês jogaram o equivalente a {timeText} em {t.Sessions} {Plural(t.Sessions, "noite", "noites")}.");
        facts.Add($"Foram {t.Matches} {Plural(t.Matches, "partida", "partidas")}: {t.Wins} {Plural(t.Wins, "vitória", "vitórias")}, " +
                  $"{t.Draws} {Plural(t.Draws, "empate", "empates")} e {t.Losses} {Plural(t.Losses, "derrota", "derrotas")}.");
        var diff = t.GoalsFor - t.GoalsAgainst;
        facts.Add($"{t.GoalsFor} {Plural(t.GoalsFor, "gol marcado", "gols marcados")} e {t.GoalsAgainst} {Plural(t.GoalsAgainst, "sofrido", "sofridos")}, " +
                  $"saldo de {(diff > 0 ? "+" : "")}{diff}.");

        if (t.CleanSheets > 0)
            facts.Add($"A defesa passou em branco em {t.CleanSheets} {Plural(t.CleanSheets, "jogo", "jogos")}.");
        if (w.Streaks.LongestWin is { Length: >= 2 } win)
            facts.Add($"A maior sequência de vitórias foi de {win.Length} jogos seguidos.");
        if (w.Players.TopScorer is { } scorer)
            facts.Add($"{scorer.Name} foi o artilheiro, com {scorer.Value:0} {Plural((int)scorer.Value, "gol", "gols")}.");
        if (w.Players.HatTricks > 0)
            facts.Add($"{(w.Players.HatTricks == 1 ? "Saiu 1 hat-trick" : $"Saíram {w.Players.HatTricks} hat-tricks")} no período.");
        if (w.Opponents.FavoriteVictim is { } victim)
            facts.Add($"{victim.Name} foi a vítima favorita: {victim.Wins} {Plural(victim.Wins, "vitória", "vitórias")} em {victim.Matches} jogos.");
        if (w.Opponents.Nemesis is { } nemesis)
            facts.Add($"{nemesis.Name} foi o maior carrasco: {nemesis.Losses} {Plural(nemesis.Losses, "derrota", "derrotas")} em {nemesis.Matches} jogos.");
        if (w.Rhythm.BusiestWeekday is { } day)
            facts.Add($"O dia favorito do grupo é {WeekdayNames[day.Weekday]}, com {day.Matches} {Plural(day.Matches, "partida", "partidas")}.");
        if (w.BestDuo is { } duo)
            facts.Add($"{duo.AssisterName} e {duo.ScorerName} formaram a dupla mais letal: {duo.Goals} gols em parceria.");

        return facts.Take(6).ToList();
    }
}
