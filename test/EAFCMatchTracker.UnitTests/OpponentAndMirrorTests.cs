using System.Net;
using EAFCMatchTracker.Application.Dtos;
using EAFCMatchTracker.Application.Exceptions;
using EAFCMatchTracker.Application.Interfaces.Services;
using EAFCMatchTracker.Application.Repositories;
using EAFCMatchTracker.Application.Services;
using EAFCMatchTracker.Domain.Entities;
using EAFCMatchTracker.Infrastructure.Data;
using EAFCMatchTracker.Infrastructure.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static EAFCMatchTracker.UnitTests.GoalTestSupport;

namespace EAFCMatchTracker.UnitTests;

internal sealed class FakeEaClubSearch : IEaClubSearchClient
{
    public EaSearchResult Result { get; set; } = new(true, Array.Empty<EaClubInfo>());
    public Task<EaSearchResult> SearchAsync(string name, CancellationToken ct) => Task.FromResult(Result);
}

/// <summary>EA falsa: devolve o JSON cujo pedaço de URL casar; null (como a EAHttpClient em erro) caso contrário.</summary>
internal sealed class FakeEaHttp : IEAHttpClient
{
    public Dictionary<string, string?> Responses { get; } = new();
    public List<string> Calls { get; } = new();

    public Task<string?> GetStringAsync(Uri endpoint, CancellationToken ct)
    {
        Calls.Add(endpoint.ToString());
        foreach (var (key, json) in Responses)
            if (endpoint.ToString().Contains(key, StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(json);
        return Task.FromResult<string?>(null);
    }

    public Task<HttpResponseMessage> SendAsync(HttpMethod method, Uri endpoint, HttpContent? content, CancellationToken ct) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
}

public class OpponentSearchTests
{
    private static EaClubInfo Ea(long id, string name, int? division = null) =>
        new(id, name, division, null, 3, 100, 10, 6, 2, 2, 20, 10, 1, 0, "crest-" + id);

    private static async Task<(EAFCContext db, OpponentSearchService svc, FakeEaClubSearch ea)> Setup()
    {
        var db = NewDb();
        var ea = new FakeEaClubSearch();
        // histórico: enfrentamos "Álmeida United" 3x, "Almería Bar" 1x e "Trash As Well" 2x
        AddOpponentMatch(db, 1, 301, "Álmeida United", T0);
        AddOpponentMatch(db, 2, 301, "Álmeida United", T0.AddDays(1));
        AddOpponentMatch(db, 3, 301, "Álmeida United", T0.AddDays(2));
        AddOpponentMatch(db, 4, 302, "Almería Bar", T0.AddDays(3));
        AddOpponentMatch(db, 5, 303, "Trash As Well", T0.AddDays(4));
        AddOpponentMatch(db, 6, 303, "Trash As Well", T0.AddDays(5));
        // partida de OUTRO clube: não pode aparecer no histórico do nosso
        db.Matches.Add(new MatchEntity
        {
            MatchId = 99, Timestamp = T0,
            Clubs = new List<MatchClubEntity>
            {
                new() { ClubId = 777, Team = 1, Details = new ClubDetailsEntity { Name = "Almost Other" } },
                new() { ClubId = 778, Team = 2, Details = new ClubDetailsEntity { Name = "Almost Another" } }
            },
            MatchPlayers = new List<MatchPlayerEntity>()
        });
        await db.SaveChangesAsync();
        return (db, new OpponentSearchService(db, ea, new MemoryCache(new MemoryCacheOptions())), ea);
    }

    private static void AddOpponentMatch(EAFCContext db, long matchId, long opponentId, string name, DateTime ts) =>
        db.Matches.Add(new MatchEntity
        {
            MatchId = matchId, Timestamp = ts,
            Clubs = new List<MatchClubEntity>
            {
                new() { ClubId = Club, Team = 1, Details = new ClubDetailsEntity { Name = "Meu clube" } },
                new() { ClubId = opponentId, Team = 2, CurrentDivision = 4, Details = new ClubDetailsEntity { Name = name, TeamId = opponentId + 1000, CrestAssetId = "custom-" + opponentId } }
            },
            MatchPlayers = new List<MatchPlayerEntity>()
        });

    [Theory]
    [InlineData("Álmeida", "almeida")]
    [InlineData("  TRASH   as-well ", "trash as well")]
    [InlineData("Ação!", "acao")]
    public void NormalizeFoldsAccentsCaseAndPunctuation(string input, string expected) =>
        Assert.Equal(expected, OpponentSearchService.Normalize(input));

    [Fact]
    public async Task QueryShorterThanTwoCharsIsRejected()
    {
        var (_, svc, _) = await Setup();
        await Assert.ThrowsAsync<DomainValidationException>(() => svc.SearchAsync("a", Club, null, default));
        await Assert.ThrowsAsync<DomainValidationException>(() => svc.SearchAsync(" ", Club, null, default));
    }

    [Fact]
    public async Task HistoryMatchesIgnoringAccentsAndCaseWithHalfAName()
    {
        var (_, svc, _) = await Setup();

        var byPrefix = await svc.SearchAsync("ALM", Club, null, default);
        Assert.Equal(new long[] { 301, 302 }, byPrefix.Results.Select(r => r.ClubId)); // 301 tem mais confrontos
        Assert.All(byPrefix.Results, r => Assert.Equal("history", r.Source));
        Assert.Equal(3, byPrefix.Results[0].TimesFaced);
        Assert.Equal(T0.AddDays(2), byPrefix.Results[0].LastFacedAt);
        Assert.Equal(4, byPrefix.Results[0].CurrentDivision);
        Assert.Equal("1301", byPrefix.Results[0].CrestAssetId); // escudo = teamId (não o crestAssetId do kit)
        Assert.Null(byPrefix.Results[0].Record);
        Assert.True(byPrefix.EaAvailable);

        var contains = await svc.SearchAsync("well", Club, null, default);
        Assert.Equal(new long[] { 303 }, contains.Results.Select(r => r.ClubId));

        var tokens = await svc.SearchAsync("well trash", Club, null, default); // todos os termos, em qualquer ordem
        Assert.Equal(new long[] { 303 }, tokens.Results.Select(r => r.ClubId));

        Assert.Empty((await svc.SearchAsync("zzz", Club, null, default)).Results);
    }

    [Fact]
    public async Task RanksStartsWithBeforeContains()
    {
        var (db, svc, _) = await Setup();
        AddOpponentMatch(db, 20, 310, "Real Almeria", T0.AddDays(9));
        await db.SaveChangesAsync();

        var r = await svc.SearchAsync("almeria", Club, null, default);
        Assert.Equal(new long[] { 302, 310 }, r.Results.Select(x => x.ClubId)); // começa com > contém
    }

    [Fact]
    public async Task MergesWithEaDedupesAndNeverReturnsOurOwnClub()
    {
        var (_, svc, ea) = await Setup();
        ea.Result = new EaSearchResult(true, new[]
        {
            Ea(301, "Almeida United", 2),   // também no histórico -> both
            Ea(400, "Alma Nova", 6),        // só EA
            Ea(Club, "Alm Meu Clube"),      // nosso clube: nunca aparece
        });

        var r = await svc.SearchAsync("alm", Club, null, default);

        Assert.Equal(new long[] { 301, 302, 400 }, r.Results.Select(x => x.ClubId)); // histórico primeiro, depois EA
        var both = r.Results[0];
        Assert.Equal("both", both.Source);
        Assert.Equal(2, both.CurrentDivision); // a divisão da EA é a atual
        Assert.Equal(3, both.ReputationTier);
        Assert.Equal(3, both.TimesFaced);
        Assert.Equal("crest-301", both.CrestAssetId);
        Assert.Equal((10, 6, 2, 2), (both.Record!.Games, both.Record.Wins, both.Record.Draws, both.Record.Losses));

        var eaOnly = r.Results[2];
        Assert.Equal("ea", eaOnly.Source);
        Assert.Equal(0, eaOnly.TimesFaced);
        Assert.Null(eaOnly.LastFacedAt);
        Assert.DoesNotContain(r.Results, x => x.ClubId == Club);
        Assert.False(r.EaTruncated);
    }

    [Fact]
    public async Task EaFailureReturnsHistoryOnlyAndTruncationIsFlagged()
    {
        var (_, svc, ea) = await Setup();

        ea.Result = EaSearchResult.Unavailable;
        var down = await svc.SearchAsync("alm", Club, null, default);
        Assert.False(down.EaAvailable);
        Assert.False(down.EaTruncated);
        Assert.Equal(2, down.Results.Count);

        ea.Result = new EaSearchResult(true, Enumerable.Range(1, 12).Select(i => Ea(500 + i, $"Alm {i:00}")).ToArray());
        var truncated = await svc.SearchAsync("alm", Club, 20, default);
        Assert.True(truncated.EaAvailable);
        Assert.True(truncated.EaTruncated);
        Assert.Equal(14, truncated.Results.Count); // 2 do histórico + 12 da EA

        var capped = await svc.SearchAsync("alm", Club, 3, default);
        Assert.Equal(3, capped.Results.Count);

        ea.Result = new EaSearchResult(true, Enumerable.Range(1, 25).Select(i => Ea(600 + i, $"Alm {i:00}")).ToArray());
        Assert.Equal(20, (await svc.SearchAsync("alm", Club, 999, default)).Results.Count); // limite máximo = 20
        Assert.Equal(15, (await svc.SearchAsync("alm", Club, null, default)).Results.Count); // padrão = 15
    }

    [Fact]
    public void EaSearchPayloadIsParsedLeniently()
    {
        const string json = """
        [
          {"clubId":"355651","wins":"10","losses":"2","ties":"3","gamesPlayed":"15","goals":"40","goalsAgainst":"20",
           "points":"250","reputationtier":"4","promotions":"2","relegations":"1","bestDivision":"1","currentDivision":"3",
           "clubInfo":{"name":"Almada FC","clubId":355651,"teamId":243,"customKit":{"crestAssetId":"999"}},"clubName":"Almada FC"},
          {"clubId":355651,"clubName":"duplicado"},
          {"clubId":"0"},
          {"clubName":"sem id"}
        ]
        """;

        var items = EaClubSearchClient.Parse(json);

        var c = Assert.Single(items);
        Assert.Equal(355651, c.ClubId);
        Assert.Equal("Almada FC", c.Name);
        Assert.Equal((3, 1, 4, 250), (c.CurrentDivision, c.BestDivision, c.ReputationTier, c.Points));
        Assert.Equal((15, 10, 3, 2), (c.GamesPlayed, c.Wins, c.Ties, c.Losses));
        Assert.Equal("243", c.CrestAssetId); // teamId, não o crestAssetId do customKit ("999")
        Assert.Empty(EaClubSearchClient.Parse("{}"));
    }
}

public class OpponentPreviewTests
{
    private static IConfiguration Config() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["EAFCSettings:BaseUrl"] = "https://ea.test/api/fc"
    }).Build();

    private static OpponentPreviewService NewService(EAFCContext db, FakeEaHttp http, FakeEaClubSearch search) =>
        new(db, http, search, Config(), new MemoryCache(new MemoryCacheOptions()), NullLogger<OpponentPreviewService>.Instance);

    private static string MatchJson(long ts, long opp, int goals, int against, int players)
    {
        var playerEntries = string.Join(",", Enumerable.Range(1, players).Select(i => $"\"{i}\":{{\"goals\":\"0\"}}"));
        return $"{{\"matchId\":\"{ts}\",\"timestamp\":{ts},\"clubs\":{{\"{opp}\":{{\"goals\":\"{goals}\",\"goalsAgainst\":\"{against}\"}}}},\"players\":{{\"{opp}\":{{{playerEntries}}}}}}}";
    }

    [Fact]
    public async Task ComposesStatsFromSearchRecentMatchesMembersAndHeadToHead()
    {
        var db = NewDb();
        // confronto direto no nosso banco: 1 vitória (2x1) e 1 derrota (0x3)
        AddMatch(db, 1, T0, 2, 1);
        AddMatch(db, 2, T0.AddDays(1), 0, 3);
        await db.SaveChangesAsync();

        var http = new FakeEaHttp();
        http.Responses["matchType=leagueMatch"] = "[" + string.Join(",", new[]
        {
            MatchJson(1000, Opponent, 1, 1, 6),   // empate (mais antiga)
            MatchJson(5000, Opponent, 4, 0, 11),  // vitória (mais nova)
            MatchJson(3000, Opponent, 0, 2, 9),   // derrota
        }) + "]";
        http.Responses["members/stats"] = "{\"members\":[{\"name\":\"a\",\"proOverall\":\"80\"},{\"name\":\"b\",\"proOverall\":\"90\"},{\"name\":\"c\",\"proOverall\":\"0\"}]}";
        var search = new FakeEaClubSearch
        {
            Result = new EaSearchResult(true, new[]
            {
                new EaClubInfo(Opponent, "Adversário FC", 2, 1, 4, 300, 20, 10, 4, 6, 50, 30, 3, 1, "crest")
            })
        };

        var dto = await NewService(db, http, search).GetPreviewAsync(Opponent, Club, "Adversário FC", default);

        Assert.Equal(Opponent, dto.ClubId);
        Assert.Equal((2, 1, 4, 300), (dto.CurrentDivision, dto.BestDivision, dto.ReputationTier, dto.Points));
        Assert.Equal((3, 1), (dto.Promotions, dto.Relegations));
        Assert.Equal((20, 10, 4, 6, 50.0), (dto.Record!.Games, dto.Record.Wins, dto.Record.Draws, dto.Record.Losses, dto.Record.WinRatePct));
        Assert.Equal((50, 30, 2.5, 1.5), (dto.Goals!.For, dto.Goals.Against, dto.Goals.AvgFor, dto.Goals.AvgAgainst));

        Assert.Equal(3, dto.Recent!.MatchesAnalyzed);
        Assert.Equal(new[] { "W", "L", "D" }, dto.Recent.Results); // mais nova primeiro
        Assert.Equal(11, dto.Recent.LastMatchPlayers);
        Assert.Equal(8.7, dto.Recent.AvgPlayersLast5);
        Assert.Equal(1.67, dto.Recent.AvgGoalsFor);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(5000).UtcDateTime, dto.Recent.LastPlayedAt);

        Assert.Equal((3, 85.0), (dto.Members!.Count, dto.Members.AvgOverall)); // overall 0 é ignorado

        var h2h = dto.HeadToHead!;
        Assert.Equal((2, 1, 0, 1, 2, 4), (h2h.Games, h2h.Wins, h2h.Draws, h2h.Losses, h2h.GoalsFor, h2h.GoalsAgainst));
        Assert.Equal(T0.AddDays(1), h2h.LastPlayedAt);
        Assert.Empty(dto.Warnings);
        // overallStats nem foi necessário: a busca já trouxe o overall
        Assert.DoesNotContain(http.Calls, c => c.Contains("overallStats"));
    }

    [Fact]
    public async Task EveryBlockIsOptionalAndGapsAreExplainedInWarnings()
    {
        var db = NewDb();
        var http = new FakeEaHttp(); // toda chamada à EA "falha" (null)
        var search = new FakeEaClubSearch { Result = EaSearchResult.Unavailable };

        var dto = await NewService(db, http, search).GetPreviewAsync(Opponent, Club, "Adversário FC", default);

        Assert.Null(dto.Record);
        Assert.Null(dto.Goals);
        Assert.Null(dto.Recent);
        Assert.Null(dto.Members);
        Assert.Null(dto.HeadToHead);
        Assert.Null(dto.CurrentDivision);
        Assert.NotEmpty(dto.Warnings);
        Assert.All(dto.Warnings, w => Assert.False(string.IsNullOrWhiteSpace(w)));
    }

    [Fact]
    public async Task FallsBackToOverallStatsAndPlayoffMatchesWhenNeeded()
    {
        var db = NewDb();
        var http = new FakeEaHttp();
        http.Responses["overallStats"] = "[{\"clubId\":\"200\",\"wins\":\"5\",\"ties\":\"1\",\"losses\":\"4\",\"goals\":\"20\",\"goalsAgainst\":\"15\",\"promotions\":\"0\",\"relegations\":\"2\",\"bestDivision\":\"3\",\"reputationtier\":\"2\"}]";
        http.Responses["matchType=leagueMatch"] = "[]";
        http.Responses["matchType=playoffMatch"] = "[" + MatchJson(2000, Opponent, 2, 1, 7) + "]";
        var search = new FakeEaClubSearch { Result = new EaSearchResult(true, Array.Empty<EaClubInfo>()) };

        var dto = await NewService(db, http, search).GetPreviewAsync(Opponent, null, "Adversário FC", default);

        Assert.Equal((10, 5, 1, 4), (dto.Record!.Games, dto.Record.Wins, dto.Record.Draws, dto.Record.Losses));
        Assert.Equal((3, 2), (dto.BestDivision, dto.ReputationTier));
        Assert.Equal(new[] { "W" }, dto.Recent!.Results);
        Assert.Null(dto.HeadToHead);
        Assert.Contains(dto.Warnings, w => w.Contains("busca da EA"));
    }

    [Fact]
    public async Task InvalidOpponentIsRejected()
    {
        var db = NewDb();
        var svc = NewService(db, new FakeEaHttp(), new FakeEaClubSearch());
        await Assert.ThrowsAsync<DomainValidationException>(() => svc.GetPreviewAsync(0, null, null, default));
    }

    private static void AddMatch(EAFCContext db, long id, DateTime ts, short ourGoals, short oppGoals) =>
        GoalTestSupport.AddMatch(db, id, ts, ourGoals, oppGoals);
}

public class ManualLinkMirrorTests
{
    private static GoalAnalysisService NewAnalysis(EAFCContext db) =>
        new(new GoalRepository(db), new MatchRepository(db), db, NullLogger<GoalAnalysisService>.Instance);

    [Fact]
    public async Task ManualGoalLinkingMirrorsIntoTheMatchRegistrationAndFlipsItToLinked()
    {
        var db = NewDb();
        var time = new FakeTimeProvider(new DateTimeOffset(T0, TimeSpan.Zero));
        AddPlayer(db, 1, Club, "Alfa");
        AddPlayer(db, 2, Club, "Beta");
        AddMatch(db, 1, T0, 2, 0, new Mp(1, 1), new Mp(2, 1, 1));
        AddMatch(db, 2, T0.AddMinutes(20), 2, 0, new Mp(1, 1), new Mp(2, 1, 1));
        await db.SaveChangesAsync();

        var svc = NewService(db, time);
        var reg = await svc.CreateAsync(new CreateGoalRegistrationRequest
        {
            ClubId = Club, OpponentClubId = Opponent, OpponentName = "Adversário FC"
        }, default);
        Assert.Equal("NeedsReview", reg.Status); // duas candidatas
        Assert.Equal(1, reg.MatchId);

        // alguém vincula os gols manualmente na página da partida (POST /api/matches/1/goals)
        await NewAnalysis(db).RegisterGoalsAsync(1, new RegisterGoalsRequest
        {
            Goals = new List<GoalRegistrationDto>
            {
                new() { ScorerPlayerEntityId = 1, AssistPlayerEntityId = 2 },
                new() { ScorerPlayerEntityId = 2 }
            }
        }, default);

        var after = (await svc.GetAsync(reg.Id, default))!;
        Assert.Equal("Linked", after.Status);
        Assert.Null(after.ReviewNote);
        Assert.Equal(new long[] { 1, 2 }, after.Goals.Select(g => g.ScorerPlayerEntityId));
        Assert.Equal(2L, after.Goals[0].AssistPlayerEntityId);
        var links = await db.MatchGoalLinks.AsNoTracking().ToListAsync();
        Assert.Equal(2, links.Count);
        Assert.All(links, l => Assert.Equal(reg.Id, l.GoalRegistrationId));

        // a resposta de GET /api/matches/{id}/goals ganha os campos do registro
        var goals = (await NewAnalysis(db).GetGoalsByMatchIdAsync(1, default))!;
        Assert.Equal(reg.Id, goals.GoalRegistrationId);
        Assert.Equal("Linked", goals.RegistrationStatus);
        Assert.Null(goals.ReviewNote);
        var plain = (await NewAnalysis(db).GetGoalsByMatchIdAsync(2, default))!;
        Assert.Null(plain.GoalRegistrationId);
        Assert.Null(plain.RegistrationStatus);
    }

    private static GoalRegistrationService NewService(EAFCContext db, FakeTimeProvider time) =>
        GoalTestSupport.NewService(db, time);
}
