using EAFCMatchTracker.Api.Controllers;
using EAFCMatchTracker.Application.Dtos;
using EAFCMatchTracker.Application.Services.Analytics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace EAFCMatchTracker.UnitTests;

/// <summary>Validação de entrada e códigos de status dos endpoints de Cartas (ProblemDetails em pt-BR).</summary>
public class PlayerCardsControllerTests
{
    private static (PlayerCardsController Controller, AnalyticsSeed Seed) Make()
    {
        var seed = new AnalyticsSeed();
        for (var i = 0; i < 3; i++)
            seed.Match(AnalyticsSeed.Base.AddDays(i), 1, 0, ours: new[] { new Pl(11), new Pl(12) });
        var service = new PlayerCardService(seed.Db, new MemoryCache(new MemoryCacheOptions()));
        return (new PlayerCardsController(service), seed);
    }

    private static int Status(IActionResult r) => r switch
    {
        ObjectResult o => o.StatusCode ?? 200,
        _ => 200
    };

    private static ProblemDetails Problem(IActionResult r) => Assert.IsType<ProblemDetails>(((ObjectResult)r).Value);

    [Fact]
    public async Task CardsReturnsOkWithTheRequestedFilters()
    {
        var (c, seed) = Make();
        using var _ = seed;

        var r = await c.Cards(AnalyticsSeed.Club, "2026-09-01", "2026-09-02", "27", "2", default);

        var dto = Assert.IsType<PlayerCardsDto>(Assert.IsType<OkObjectResult>(r).Value);
        Assert.Equal((2, 2, 2), (dto.TotalMatches, dto.MinMatches, dto.Cards.Count));
        Assert.Equal(27, dto.GameVersion);
    }

    [Fact]
    public async Task CardsDefaultsMinMatchesToThree()
    {
        var (c, seed) = Make();
        using var _ = seed;
        var dto = Assert.IsType<PlayerCardsDto>(Assert.IsType<OkObjectResult>(await c.Cards(AnalyticsSeed.Club, null, null, null, null, default)).Value);
        Assert.Equal(3, dto.MinMatches);
        Assert.Equal(2, dto.Cards.Count); // os dois jogaram 3 partidas
    }

    [Theory]
    [InlineData(0L, null, null, null, null)]
    [InlineData(-5L, null, null, null, null)]
    [InlineData(100L, "2026-13-01", null, null, null)]
    [InlineData(100L, "01/09/2026", null, null, null)]
    [InlineData(100L, null, "ontem", null, null)]
    [InlineData(100L, "2026-09-10", "2026-09-01", null, null)]      // from > to
    [InlineData(100L, "2020-01-01", "2026-09-01", null, null)]      // mais de 5 anos
    [InlineData(100L, null, null, "abc", null)]
    [InlineData(100L, null, null, "0", null)]
    [InlineData(100L, null, null, null, "muitos")]
    public async Task CardsRejectsInvalidInputWith400(long clubId, string? from, string? to, string? version, string? min)
    {
        var (c, seed) = Make();
        using var _ = seed;

        var r = await c.Cards(clubId, from, to, version, min, default);

        Assert.Equal(400, Status(r));
        Assert.False(string.IsNullOrWhiteSpace(Problem(r).Detail));
    }

    [Fact]
    public async Task CompareReturnsOkForTwoClubPlayers()
    {
        var (c, seed) = Make();
        using var _ = seed;

        var r = await c.Compare(AnalyticsSeed.Club, "11", "12", null, null, null, default);

        var dto = Assert.IsType<PlayerCompareDto>(Assert.IsType<OkObjectResult>(r).Value);
        Assert.Equal((11L, 12L), (dto.A.PlayerEntityId, dto.B.PlayerEntityId));
        Assert.Equal(3, dto.Together!.Matches);
    }

    [Fact]
    public async Task CompareWithTheSamePlayerTwiceIs400()
    {
        var (c, seed) = Make();
        using var _ = seed;

        var r = await c.Compare(AnalyticsSeed.Club, "11", "11", null, null, null, default);

        Assert.Equal(400, Status(r));
        Assert.Contains("diferentes", Problem(r).Detail);
    }

    [Theory]
    [InlineData(null, "12")]
    [InlineData("11", null)]
    [InlineData("", "12")]
    [InlineData("x", "12")]
    [InlineData("11", "1.5")]
    [InlineData("0", "12")]
    [InlineData("-3", "12")]
    public async Task CompareRejectsMissingOrInvalidIdsWith400(string? a, string? b)
    {
        var (c, seed) = Make();
        using var _ = seed;
        Assert.Equal(400, Status(await c.Compare(AnalyticsSeed.Club, a, b, null, null, null, default)));
    }

    [Fact]
    public async Task CompareRejectsInvalidRangeOrVersionWith400()
    {
        var (c, seed) = Make();
        using var _ = seed;
        Assert.Equal(400, Status(await c.Compare(AnalyticsSeed.Club, "11", "12", "2026-09-10", "2026-09-01", null, default)));
        Assert.Equal(400, Status(await c.Compare(AnalyticsSeed.Club, "11", "12", "2019-01-01", "2026-01-01", null, default)));
        Assert.Equal(400, Status(await c.Compare(AnalyticsSeed.Club, "11", "12", null, null, "abc", default)));
        Assert.Equal(400, Status(await c.Compare(0, "11", "12", null, null, null, default)));
    }

    [Fact]
    public async Task CompareReturns404WhenAPlayerIsNotInTheClub()
    {
        var (c, seed) = Make();
        using var _ = seed;

        var r = await c.Compare(AnalyticsSeed.Club, "11", "999", null, null, null, default);
        Assert.Equal(404, Status(r));
        Assert.Equal("Jogador não encontrado neste clube.", Problem(r).Detail);

        // clube diferente: os jogadores 11 e 12 não pertencem ao clube 555
        Assert.Equal(404, Status(await c.Compare(555, "11", "12", null, null, null, default)));
    }
}
