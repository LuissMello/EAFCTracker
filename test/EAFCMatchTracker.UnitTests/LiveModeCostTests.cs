using EAFCMatchTracker.Application.Interfaces.Repositories;
using EAFCMatchTracker.Application.Interfaces.Services;
using EAFCMatchTracker.Application.Repositories;
using EAFCMatchTracker.Application.Services;
using EAFCMatchTracker.Domain.Entities;
using EAFCMatchTracker.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EAFCMatchTracker.UnitTests;

/// <summary>Redução de custo: cache do agendamento (5 min), tick adaptativo, ciclo do boot só se devido e keep-alive sem banco.</summary>
public class LiveModeCostTests
{
    // ------------------------------------------------------------------ doubles

    private sealed class ManualTime : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now += by;
    }

    private sealed class CountingRepo : IAppSettingRepository
    {
        public int Reads;
        public Dictionary<string, string> Values = new();
        public TaskCompletionSource? Gate; // se definido, a leitura espera por ele (simula leitura lenta)

        public async Task<List<AppSettingEntity>> GetAllAsync(CancellationToken ct)
        {
            Interlocked.Increment(ref Reads);
            var snapshot = Values.Select(kv => new AppSettingEntity { Key = kv.Key, Value = kv.Value }).ToList();
            if (Gate is not null) await Gate.Task;
            return snapshot;
        }

        public Task<AppSettingEntity?> GetByKeyAsync(string key, CancellationToken ct) =>
            Task.FromResult<AppSettingEntity?>(Values.TryGetValue(key, out var v) ? new AppSettingEntity { Key = key, Value = v } : null);

        public Task UpsertAsync(string key, string value, CancellationToken ct)
        {
            Values[key] = value;
            return Task.CompletedTask;
        }
    }

    private static (LiveModeService Live, CountingRepo Repo, ManualTime Time) Make()
    {
        var repo = new CountingRepo();
        var services = new ServiceCollection();
        services.AddSingleton<IAppSettingRepository>(repo);
        var factory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        var time = new ManualTime();
        return (new LiveModeService(factory, time), repo, time);
    }

    // ------------------------------------------------------------------ cache do agendamento

    [Fact]
    public async Task SecondCallWithinTheTtlDoesNotHitTheRepository()
    {
        var (live, repo, time) = Make();

        var a = await live.GetScheduleAsync(default);
        time.Advance(TimeSpan.FromMinutes(4) + TimeSpan.FromSeconds(59));
        var b = await live.GetScheduleAsync(default);
        var c = await live.GetStatusAsync(default);

        Assert.Equal(1, repo.Reads);
        Assert.Same(a, b);
        Assert.Equal(a.Live, c);
    }

    [Fact]
    public async Task TtlExpiryReloadsFromTheRepository()
    {
        var (live, repo, time) = Make();
        Assert.False((await live.GetStatusAsync(default)).Enabled);

        repo.Values[AppSettingEntity.Keys.LiveEnabled] = "true"; // alterado por outro processo: só aparece após o TTL
        time.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));

        Assert.True((await live.GetStatusAsync(default)).Enabled);
        Assert.Equal(2, repo.Reads);
    }

    [Fact]
    public async Task SetInvalidatesSoTheTogglerNeverSeesAStaleValue()
    {
        var (live, repo, _) = Make();
        Assert.False((await live.GetStatusAsync(default)).Enabled); // cache preenchido com "desligado"

        var after = await live.SetAsync(true, default);
        Assert.True(after.Enabled);
        Assert.True((await live.GetStatusAsync(default)).Enabled);  // GET seguinte já vê o novo valor

        Assert.False((await live.SetAsync(false, default)).Enabled);
        Assert.False((await live.GetScheduleAsync(default)).Live.Enabled);
        Assert.Equal(3, repo.Reads); // 1 inicial + 1 por Set; os GET depois do Set vêm do cache
    }

    [Fact]
    public async Task InvalidateDuringAnInFlightReadDoesNotCacheTheOldValue()
    {
        var (live, repo, _) = Make();
        repo.Gate = new TaskCompletionSource();
        var inFlight = live.GetScheduleAsync(default);          // leitura lenta (valor antigo: desligado)

        repo.Values[AppSettingEntity.Keys.LiveEnabled] = "true"; // alguém liga e invalida no meio da leitura
        live.Invalidate();
        repo.Gate.SetResult();
        Assert.False((await inFlight).Live.Enabled);             // quem já estava lendo recebe o que leu...

        repo.Gate = null;
        Assert.True((await live.GetStatusAsync(default)).Enabled); // ...mas o valor velho NÃO ficou em cache
    }

    [Fact]
    public async Task AppSettingRepositoryUpsertInvalidatesTheCacheForEveryWriter()
    {
        using var seed = new AnalyticsSeed();
        LiveModeService? live = null;
        var services = new ServiceCollection();
        services.AddDbContext<EAFCContext>(o => o.UseInMemoryDatabase(seed.DbName));
        services.AddScoped<IAppSettingRepository>(sp => new AppSettingRepository(sp.GetRequiredService<EAFCContext>(), live));
        live = new LiveModeService(services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>());

        Assert.Equal(60, (await live.GetScheduleAsync(default)).FetchIntervalMinutes);

        // caminho do PUT /api/admin/settings: grava direto no repositório, sem passar pelo LiveModeService
        using (var db = new EAFCContext(new DbContextOptionsBuilder<EAFCContext>().UseInMemoryDatabase(seed.DbName).Options))
            await new AppSettingRepository(db, live).UpsertAsync(AppSettingEntity.Keys.FetchIntervalMinutes, "15", default);

        Assert.Equal(15, (await live.GetScheduleAsync(default)).FetchIntervalMinutes);
    }

    // ------------------------------------------------------------------ acordar o loop

    [Fact]
    public async Task WaitForChangeTimesOutWakesOnInvalidateAndHonorsCancellation()
    {
        var (live, _, _) = Make();

        Assert.False(await live.WaitForChangeAsync(TimeSpan.FromMilliseconds(30), default)); // nada mudou: estoura o tempo

        var waiting = live.WaitForChangeAsync(TimeSpan.FromSeconds(30), default);             // sono longo...
        live.Invalidate();                                                                     // ...cortado pela mudança
        Assert.True(await waiting.WaitAsync(TimeSpan.FromSeconds(5)));

        live.Invalidate();                                                                     // sinal pendente antes de esperar também vale
        Assert.True(await live.WaitForChangeAsync(TimeSpan.FromSeconds(30), default));

        using var cts = new CancellationTokenSource();
        var cancelled = live.WaitForChangeAsync(TimeSpan.FromSeconds(30), cts.Token);
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
    }

    // ------------------------------------------------------------------ loop de busca

    private sealed class FakeLive : ILiveModeService
    {
        public FetchSchedule Schedule = new(new LiveStatus(false, null, 5), 60, 4);
        public readonly List<TimeSpan> Waits = new();
        public int MaxWaits = 3;
        public Action<int>? OnWait;
        public int ScheduleReads;

        public Task<LiveStatus> GetStatusAsync(CancellationToken ct) => Task.FromResult(Schedule.Live);
        public Task<LiveStatus> SetAsync(bool enabled, CancellationToken ct) => throw new NotSupportedException();
        public Task<FetchSchedule> GetScheduleAsync(CancellationToken ct) { ScheduleReads++; return Task.FromResult(Schedule); }
        public void Invalidate() { }

        public Task<bool> WaitForChangeAsync(TimeSpan timeout, CancellationToken ct)
        {
            Waits.Add(timeout);
            OnWait?.Invoke(Waits.Count);
            if (Waits.Count > MaxWaits) throw new OperationCanceledException(); // encerra o loop
            return Task.FromResult(false);
        }
    }

    private sealed class FakeCoordinator : IFetchCoordinator
    {
        public int Runs;
        public bool IsRunning => false;
        public DateTimeOffset? LastCycleStartedUtc => null;
        public Task<FetchRunResult> RunManualAsync(CancellationToken ct) => RunScheduledAsync(ct);
        public Task<FetchRunResult> RunScheduledAsync(CancellationToken ct)
        {
            Runs++;
            return Task.FromResult(new FetchRunResult(DateTimeOffset.UtcNow, false, Array.Empty<string>()));
        }
    }

    private sealed class StubHttpFactory : IHttpClientFactory
    {
        public int Calls;
        private sealed class Handler(StubHttpFactory owner) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                Interlocked.Increment(ref owner.Calls);
                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
            }
        }
        public HttpClient CreateClient(string name) => new(new Handler(this));
    }

    private sealed class TestWorker : ClubMatchBackgroundService
    {
        public DateTimeOffset? LastPersisted;
        public bool ThrowOnRead;

        public TestWorker(FakeCoordinator c, FakeLive live, StubHttpFactory http, IConfiguration config)
            : base(c, live, http, config, NullLogger<ClubMatchBackgroundService>.Instance) { }

        protected internal override Task<DateTimeOffset?> GetLastPersistedCycleAsync(CancellationToken ct) =>
            ThrowOnRead ? throw new InvalidOperationException("tabela ausente") : Task.FromResult(LastPersisted);

        public Task RunAsync() => ExecuteAsync(CancellationToken.None);
    }

    private static IConfiguration Config(string? keepAliveUrl = null) => new ConfigurationBuilder()
        .AddInMemoryCollection(keepAliveUrl is null ? new Dictionary<string, string?>() : new() { ["Live:KeepAliveUrl"] = keepAliveUrl })
        .Build();

    private static (TestWorker Worker, FakeCoordinator Coord, FakeLive Live, StubHttpFactory Http) MakeWorker(
        DateTimeOffset? lastPersisted, int liveInterval = 5, bool live = false)
    {
        var coord = new FakeCoordinator();
        var fake = new FakeLive { Schedule = new FetchSchedule(new LiveStatus(live, null, liveInterval), 60, 4), MaxWaits = 0 };
        var http = new StubHttpFactory();
        return (new TestWorker(coord, fake, http, Config("http://keepalive.test/health/live")) { LastPersisted = lastPersisted }, coord, fake, http);
    }

    [Fact]
    public async Task BootCycleIsSkippedWhenTheLastPersistedCycleIsRecent()
    {
        var (w, coord, _, _) = MakeWorker(DateTimeOffset.UtcNow.AddMinutes(-2)); // intervalo normal: 60 min
        await w.RunAsync();
        Assert.Equal(0, coord.Runs);
    }

    [Theory]
    [InlineData(-180)]   // antigo
    [InlineData(-61)]    // logo além do intervalo
    public async Task BootCycleRunsWhenTheLastCycleIsOld(int minutesAgo)
    {
        var (w, coord, _, _) = MakeWorker(DateTimeOffset.UtcNow.AddMinutes(minutesAgo));
        await w.RunAsync();
        Assert.Equal(1, coord.Runs);
    }

    [Fact]
    public async Task BootCycleNeverSkipsWithoutAPersistedTimestampOrWhenReadingFailsOrClockIsInTheFuture()
    {
        var (missing, c1, _, _) = MakeWorker(null);
        await missing.RunAsync();
        Assert.Equal(1, c1.Runs);

        var (broken, c2, _, _) = MakeWorker(DateTimeOffset.UtcNow.AddMinutes(-1));
        broken.ThrowOnRead = true;
        await broken.RunAsync();
        Assert.Equal(1, c2.Runs);

        var (future, c3, _, _) = MakeWorker(DateTimeOffset.UtcNow.AddHours(3));
        await future.RunAsync();
        Assert.Equal(1, c3.Runs);
    }

    [Fact]
    public async Task BootUsesTheLiveIntervalWhenLiveModeIsOn()
    {
        var (recent, c1, _, _) = MakeWorker(DateTimeOffset.UtcNow.AddMinutes(-2), liveInterval: 5, live: true);
        await recent.RunAsync();
        Assert.Equal(0, c1.Runs);                       // 2 min < 5 min

        var (old, c2, _, _) = MakeWorker(DateTimeOffset.UtcNow.AddMinutes(-10), liveInterval: 5, live: true);
        await old.RunAsync();
        Assert.Equal(1, c2.Runs);                       // 10 min >= 5 min (mesmo sendo menor que os 60 min normais)
    }

    [Fact]
    public async Task TickIsSixtySecondsWhenIdleAndTwentyWhenLiveAndSwitchesWithTheSettings()
    {
        var (idle, _, fakeIdle, _) = MakeWorker(DateTimeOffset.UtcNow.AddMinutes(-2));
        fakeIdle.MaxWaits = 3;
        await idle.RunAsync();
        Assert.Equal(new[] { ClubMatchBackgroundService.IdleTick, ClubMatchBackgroundService.IdleTick, ClubMatchBackgroundService.IdleTick, ClubMatchBackgroundService.IdleTick },
            fakeIdle.Waits);
        Assert.Equal(TimeSpan.FromSeconds(60), ClubMatchBackgroundService.IdleTick);
        Assert.Equal(TimeSpan.FromSeconds(20), ClubMatchBackgroundService.LiveTick);

        var (live, _, fakeLive, _) = MakeWorker(DateTimeOffset.UtcNow.AddMinutes(-2), live: true);
        fakeLive.MaxWaits = 2;
        await live.RunAsync();
        Assert.All(fakeLive.Waits, t => Assert.Equal(ClubMatchBackgroundService.LiveTick, t));

        // liga durante a espera: o tick seguinte passa a ser de 20 s
        var (toggle, _, fakeToggle, _) = MakeWorker(DateTimeOffset.UtcNow.AddMinutes(-2));
        fakeToggle.MaxWaits = 3;
        fakeToggle.OnWait = n => { if (n == 2) fakeToggle.Schedule = new FetchSchedule(new LiveStatus(true, null, 5), 60, 4); };
        await toggle.RunAsync();
        Assert.Equal(new[] { 60, 60, 20, 20 }, fakeToggle.Waits.Select(t => (int)t.TotalSeconds));
    }

    [Fact]
    public async Task LiveJustSwitchedOnRunsACycleOnTheNextTickAndKeepAlivePingsOnlyWhileLive()
    {
        var (w, coord, fake, http) = MakeWorker(DateTimeOffset.UtcNow.AddMinutes(-2)); // boot pulado (2 min < 60)
        fake.MaxWaits = 2;
        fake.OnWait = n => { if (n == 2) fake.Schedule = new FetchSchedule(new LiveStatus(true, null, 5), 60, 4); };
        await w.RunAsync();

        Assert.Equal(1, coord.Runs);   // ligado com o último ciclo há > 60 s: roda de imediato, sem esperar o intervalo
        Assert.Equal(1, http.Calls);   // keep-alive só depois que o modo ao vivo ligou

        var (off, _, _, httpOff) = MakeWorker(DateTimeOffset.UtcNow.AddMinutes(-2));
        await off.RunAsync();
        Assert.Equal(0, httpOff.Calls);
    }

    [Fact]
    public void KeepAliveUrlDefaultsToTheDatabaseFreeLivenessEndpoint()
    {
        Assert.Equal("https://meu-app.fly.dev/health/live", ClubMatchBackgroundService.ResolveKeepAliveUrl(Config(), " meu-app "));
        Assert.Equal("http://x.test/ping", ClubMatchBackgroundService.ResolveKeepAliveUrl(Config(" http://x.test/ping "), "meu-app")); // override
        Assert.Null(ClubMatchBackgroundService.ResolveKeepAliveUrl(Config(), null));
        Assert.Null(ClubMatchBackgroundService.ResolveKeepAliveUrl(Config(), "  "));
    }
}
