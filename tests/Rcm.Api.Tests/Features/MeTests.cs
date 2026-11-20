using Rcm.Api.Auth;
using Rcm.Api.Data;
using Rcm.Api.Domain;
using Rcm.Api.Features;
using Rcm.Api.Features.Me;
using Rcm.Api.Tests.TestSupport;

namespace Rcm.Api.Tests.Features;

public class MeTests
{
    [Fact]
    public async Task HandleAsync_SeededBiller_ReturnsRoleManagerAndCovers()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();

        var me = await Handler(db, database).HandleAsync(new CurrentUser(SeedData.Alex, "Alex", Role.Biller), CancellationToken.None);

        Assert.NotNull(me);
        Assert.Equal((SeedData.Alex, "Alex", Role.Biller, (int?)SeedData.Sam), (me.Id, me.Name, me.Role, me.ReportsToId));
        Assert.Equal(["Blake"], me.CoveringFor.Select(p => p.Name));
    }

    [Fact]
    public async Task HandleAsync_SupervisorCoveringSupervisor_ListsThem()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();

        var me = await Handler(db, database).HandleAsync(new CurrentUser(SeedData.Sam, "Sam", Role.Supervisor), CancellationToken.None);

        Assert.Equal(["Taylor"], me?.CoveringFor.Select(p => p.Name));
    }

    [Fact]
    public async Task HandleAsync_Manager_HasNoManagerAndCoversNobody()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();

        var me = await Handler(db, database).HandleAsync(new CurrentUser(SeedData.Morgan, "Morgan", Role.Manager), CancellationToken.None);

        Assert.NotNull(me);
        Assert.Null(me.ReportsToId);
        Assert.Empty(me.CoveringFor);
    }

    [Fact]
    public async Task HandleAsync_UnknownUser_ReturnsNull()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();

        var me = await Handler(db, database).HandleAsync(new CurrentUser(999, "Nobody", Role.Biller), CancellationToken.None);

        Assert.Null(me);
    }

    private static GetMeHandler Handler(AppDbContext db, SqliteTestDatabase database) =>
        new(db, new RequestContextLoader(db, database.Calendar));
}
