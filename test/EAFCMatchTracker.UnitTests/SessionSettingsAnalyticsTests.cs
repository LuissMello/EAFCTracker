using EAFCMatchTracker.Application.Services;
using EAFCMatchTracker.Application.Services.Analytics;
using EAFCMatchTracker.Domain.Entities;
using Xunit;

namespace EAFCMatchTracker.UnitTests;

/// <summary>
/// Uma "noite de jogo" é exatamente o que o ClubSessionService produz a partir da parametrização do admin
/// (gap, fuso, fronteiras manuais). Estes testes usam serviços com CACHE COMPARTILHADO e alteram a parametrização
/// entre as chamadas, SEM partidas novas: o resultado das três análises precisa mudar na hora.
/// </summary>
public class SessionSettingsAnalyticsTests
{
    private static DateTime Utc(int month, int day, int hour, int minute = 0) =>
        new(2026, month, day, hour, minute, 0, DateTimeKind.Utc);

    private sealed class Rig : IDisposable
    {
        public AnalyticsSeed Seed { get; } = new();
        public long[] Ids { get; } = new long[4];
        public GameNightService Nights { get; }
        public LabService Lab { get; }
        public WrappedService Wrapped { get; }

        public Rig()
        {
            // segunda 28/09 (SP): 22:00, 23:30, 00:50(+1) com intervalos de 90 e 80 min; depois quinta 01/10 22:00.
            Ids[0] = Seed.Match(Utc(9, 29, 1), 2, 0, ourSr: 1500);
            Ids[1] = Seed.Match(Utc(9, 29, 2, 30), 1, 1, ourSr: 1505);
            Ids[2] = Seed.Match(Utc(9, 29, 3, 50), 0, 1, ourSr: 1495);
            Ids[3] = Seed.Match(Utc(10, 2, 1), 3, 0, ourSr: 1510);
            var cache = Seed.Cache;
            var sessions = new ClubSessionService(Seed.Db);
            Nights = new GameNightService(Seed.Db, sessions, cache);
            Lab = new LabService(Seed.Db, sessions, cache);
            Wrapped = new WrappedService(Seed.Db, sessions, cache);
        }

        public void SetGap(int minutes)
        {
            Seed.Db.TrackedClubs.Find(AnalyticsSeed.Club)!.SessionGapMinutes = minutes;
            Seed.Db.SaveChanges();
        }

        public void SetZone(string tz)
        {
            Seed.Db.TrackedClubs.Find(AnalyticsSeed.Club)!.TimeZoneId = tz;
            Seed.Db.SaveChanges();
        }

        public void Boundary(long matchId, bool startsNew)
        {
            var row = Seed.Db.SessionBoundaries.FirstOrDefault(b => b.ClubId == AnalyticsSeed.Club && b.MatchId == matchId);
            if (row is null) Seed.Db.SessionBoundaries.Add(new SessionBoundaryEntity { ClubId = AnalyticsSeed.Club, MatchId = matchId, StartNewSession = startsNew });
            else row.StartNewSession = startsNew;
            Seed.Db.SaveChanges();
        }

        public void ClearBoundaries()
        {
            Seed.Db.SessionBoundaries.RemoveRange(Seed.Db.SessionBoundaries.Where(b => b.ClubId == AnalyticsSeed.Club));
            Seed.Db.SaveChanges();
        }

        public async Task<(int Nights, int LabPos1, int WrappedSessions)> SnapshotAsync()
        {
            var nights = await Nights.GetNightsAsync(AnalyticsSeed.Club, null, default);
            var lab = await Lab.GetContextAsync(AnalyticsSeed.Club, null, null, null, default);
            var wrapped = await Wrapped.GetWrappedAsync(AnalyticsSeed.Club, null, default);
            return (nights.Nights.Count, lab.BySessionPosition.FirstOrDefault(p => p.Position == 1)?.Matches ?? 0, wrapped.Totals.Sessions);
        }

        public void Dispose() => Seed.Dispose();
    }

    [Fact]
    public async Task ChangingTheGapSplitsTheNightInAllThreeApisWithoutNewMatches()
    {
        using var rig = new Rig();

        // gap padrão (120): 22:00 → 00:50 é UMA noite (3 jogos) + a de quinta
        Assert.Equal((2, 2, 2), await rig.SnapshotAsync());
        var night = (await rig.Nights.GetNightsAsync(AnalyticsSeed.Club, null, default)).Nights[0];
        Assert.Equal(3, night.Matches);

        rig.SetGap(30); // cada partida vira uma noite
        Assert.Equal((4, 4, 4), await rig.SnapshotAsync());
        var list = await rig.Nights.GetNightsAsync(AnalyticsSeed.Club, null, default);
        Assert.Equal(rig.Ids, list.Nights.Select(n => n.SessionId));
        Assert.Equal(new[] { 1, 1, 1, 1 }, list.Nights.Select(n => n.Matches));

        rig.SetGap(85); // 90 min separa 1→2 (divide); 80 min une 2→3: [1] [2,3] [4]
        Assert.Equal((3, 3, 3), await rig.SnapshotAsync());

        rig.SetGap(120); // volta ao padrão: resultado idêntico ao inicial
        Assert.Equal((2, 2, 2), await rig.SnapshotAsync());
    }

    [Fact]
    public async Task ManualSplitAndJoinOverrideTheGapAndInvalidateTheCache()
    {
        using var rig = new Rig();
        Assert.Equal((2, 2, 2), await rig.SnapshotAsync());

        rig.Boundary(rig.Ids[1], startsNew: true); // split no meio da noite
        Assert.Equal((3, 3, 3), await rig.SnapshotAsync());
        var list = await rig.Nights.GetNightsAsync(AnalyticsSeed.Club, null, default);
        Assert.Equal(new[] { 1, 2, 1 }, list.Nights.Select(n => n.Matches));

        rig.ClearBoundaries();
        Assert.Equal((2, 2, 2), await rig.SnapshotAsync());

        rig.Boundary(rig.Ids[3], startsNew: false); // join: a partida de quinta entra na noite anterior apesar do intervalo enorme
        Assert.Equal((1, 1, 1), await rig.SnapshotAsync());
        var joined = await rig.Nights.GetNightsAsync(AnalyticsSeed.Club, null, default);
        Assert.Equal(4, joined.Nights.Single().Matches);

        rig.SetGap(30); // join vence o gap; as demais continuam por gap
        rig.Boundary(rig.Ids[3], startsNew: false);
        Assert.Equal((3, 3, 3), await rig.SnapshotAsync());

        rig.ClearBoundaries();
        rig.SetGap(120);
        Assert.Equal((2, 2, 2), await rig.SnapshotAsync());
    }

    [Fact]
    public async Task ChangingTheTimeZoneChangesTheLocalDateAndTheWeekdayHourBuckets()
    {
        using var rig = new Rig();

        var spNights = await rig.Nights.GetNightsAsync(AnalyticsSeed.Club, null, default);
        Assert.Equal("America/Sao_Paulo", spNights.TimeZoneId);
        Assert.Equal(new DateOnly(2026, 9, 28), spNights.Nights[0].Date); // segunda 22:00 local
        var spCtx = await rig.Lab.GetContextAsync(AnalyticsSeed.Club, null, null, null, default);
        Assert.Contains(spCtx.ByWeekday, w => w.Weekday == 1 && w.Matches == 2); // 22:00 e 23:30 de segunda
        Assert.Contains(spCtx.ByHour, h => h.Hour == 22);
        var spWrapped = await rig.Wrapped.GetWrappedAsync(AnalyticsSeed.Club, null, default);

        rig.SetZone("UTC");

        var utcNights = await rig.Nights.GetNightsAsync(AnalyticsSeed.Club, null, default);
        Assert.Equal("UTC", utcNights.TimeZoneId);
        Assert.Equal(new DateOnly(2026, 9, 29), utcNights.Nights[0].Date); // 01:00 de terça
        Assert.Equal(spNights.Nights.Count, utcNights.Nights.Count);
        var utcCtx = await rig.Lab.GetContextAsync(AnalyticsSeed.Club, null, null, null, default);
        Assert.Equal("UTC", utcCtx.TimeZoneId);
        Assert.Contains(utcCtx.ByWeekday, w => w.Weekday == 2 && w.Matches == 3);
        Assert.Contains(utcCtx.ByHour, h => h.Hour == 1);
        Assert.DoesNotContain(utcCtx.ByHour, h => h.Hour == 22);
        var utcWrapped = await rig.Wrapped.GetWrappedAsync(AnalyticsSeed.Club, null, default);
        Assert.Equal("UTC", utcWrapped.TimeZoneId);
        Assert.NotEqual(spWrapped.From, utcWrapped.From);
    }
}
