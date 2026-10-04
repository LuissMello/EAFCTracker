using EAFCMatchTracker.Api.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace EAFCMatchTracker.UnitTests;

public class ReadOnlyModeTests
{
    [Theory]
    [InlineData("INSERT INTO \"Matches\" (\"MatchId\") VALUES (1)", true)]
    [InlineData("  update \"Matches\" SET \"Timestamp\" = now()", true)]
    [InlineData("DELETE FROM \"Matches\" WHERE \"MatchId\" = 1", true)]
    [InlineData("ALTER TABLE \"Matches\" ADD COLUMN x int", true)]
    [InlineData("CREATE TABLE \"X\" (id int)", true)]
    [InlineData("DROP TABLE \"X\"", true)]
    [InlineData("TRUNCATE \"Matches\"", true)]
    [InlineData("-- comentário\nINSERT INTO \"A\" VALUES (1)", true)]
    [InlineData("SELECT * FROM \"Matches\"", false)]
    [InlineData("  select count(*) from \"Matches\"", false)]
    [InlineData("SELECT \"UpdatedAtUtc\", \"DeletedFlag\" FROM \"OverallStats\"", false)]
    [InlineData("WITH x AS (SELECT 1) SELECT * FROM x", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsWrite_RecognizesWriteStatementsOnly(string? sql, bool expected)
        => Assert.Equal(expected, ReadOnlyCommandInterceptor.IsWrite(sql));

    [Fact]
    public void IsWrite_DoesNotMatchIdentifiersThatMerelyStartWithAVerb()
        => Assert.False(ReadOnlyCommandInterceptor.IsWrite("SELECT 1"));

    [Theory]
    [InlineData("GET", true)]
    [InlineData("HEAD", true)]
    [InlineData("OPTIONS", true)]
    [InlineData("POST", false)]
    [InlineData("PUT", false)]
    [InlineData("DELETE", false)]
    [InlineData("PATCH", false)]
    public async Task Middleware_AllowsOnlyReadMethods(string method, bool allowed)
    {
        var called = false;
        var middleware = new ReadOnlyGuardMiddleware(_ => { called = true; return Task.CompletedTask; });
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal(allowed, called);
        if (!allowed)
        {
            Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
            context.Response.Body.Position = 0;
            var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
            Assert.Contains("read_only_mode", body);
        }
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("false", false)]
    [InlineData(null, false)]
    public void IsEnabled_ReadsTheRuntimeSetting(string? value, bool expected)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(value is null ? new Dictionary<string, string?>() : new Dictionary<string, string?> { [ReadOnlyMode.ConfigKey] = value })
            .Build();
        Assert.Equal(expected, ReadOnlyMode.IsEnabled(config));
    }
}
