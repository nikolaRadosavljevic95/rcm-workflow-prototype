using Rcm.Api.Auth;
using Rcm.Api.Data;
using Rcm.Api.Domain;
using Rcm.Api.Features.Me;
using Rcm.Api.Tests.TestSupport;

namespace Rcm.Api.Tests.Features;

public class MeTests
{
    [Fact]
    public async Task HandleAsync_SeededBiller_ReturnsRoleAndManager()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();

        var me = await new GetMeHandler(db).HandleAsync(new CurrentUser(SeedData.Alex, "Alex", Role.Biller), CancellationToken.None);

        Assert.Equal(new MeResponse(SeedData.Alex, "Alex", Role.Biller, SeedData.Sam), me);
    }

    [Fact]
    public async Task HandleAsync_Manager_HasNoManager()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();

        var me = await new GetMeHandler(db).HandleAsync(new CurrentUser(SeedData.Morgan, "Morgan", Role.Manager), CancellationToken.None);

        Assert.NotNull(me);
        Assert.Null(me.ReportsToId);
    }

    [Fact]
    public async Task HandleAsync_UnknownUser_ReturnsNull()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();

        var me = await new GetMeHandler(db).HandleAsync(new CurrentUser(999, "Nobody", Role.Biller), CancellationToken.None);

        Assert.Null(me);
    }
}
