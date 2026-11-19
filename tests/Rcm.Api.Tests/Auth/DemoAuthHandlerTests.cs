using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Rcm.Api.Auth;
using Rcm.Api.Data;
using Rcm.Api.Tests.TestSupport;

namespace Rcm.Api.Tests.Auth;

public class DemoAuthHandlerTests
{
    [Fact]
    public async Task AuthenticateAsync_ActiveUserId_IssuesIdNameAndRoleClaims()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);

        var result = await AuthenticateAsync(database, SeedData.Alex.ToString());

        Assert.True(result.Succeeded);
        var principal = result.Principal!;
        Assert.Equal(SeedData.Alex.ToString(), principal.FindFirstValue(CurrentUser.UserIdClaim));
        Assert.Equal("Alex", principal.FindFirstValue(ClaimTypes.Name));
        Assert.Equal("Biller", principal.FindFirstValue(ClaimTypes.Role));
    }

    [Fact]
    public async Task AuthenticateAsync_NoHeader_ReturnsNoResult()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);

        var result = await AuthenticateAsync(database, header: null);

        Assert.True(result.None);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("-7")]
    [InlineData("7,8")]
    [InlineData("999")]
    public async Task AuthenticateAsync_InvalidOrUnknownId_Fails(string header)
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);

        var result = await AuthenticateAsync(database, header);

        Assert.NotNull(result.Failure);
    }

    [Fact]
    public async Task AuthenticateAsync_InactiveUser_Fails()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using (var db = database.CreateContext())
        {
            await db.Users.Where(u => u.Id == SeedData.Blake).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, false));
        }

        var result = await AuthenticateAsync(database, SeedData.Blake.ToString());

        Assert.NotNull(result.Failure);
    }

    private static async Task<AuthenticateResult> AuthenticateAsync(SqliteTestDatabase database, string? header)
    {
        await using var db = database.CreateContext();
        var handler = new DemoAuthHandler(
            new StaticOptionsMonitor<AuthenticationSchemeOptions>(new AuthenticationSchemeOptions()),
            NullLoggerFactory.Instance,
            UrlEncoder.Default,
            db);
        var context = new DefaultHttpContext();
        if (header is not null)
        {
            context.Request.Headers[DemoAuthHandler.HeaderName] = header;
        }

        await handler.InitializeAsync(new AuthenticationScheme(DemoAuthHandler.SchemeName, null, typeof(DemoAuthHandler)), context);
        return await handler.AuthenticateAsync();
    }

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;

        public T Get(string? name) => value;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
