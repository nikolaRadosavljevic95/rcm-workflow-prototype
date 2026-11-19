using Microsoft.EntityFrameworkCore;
using Rcm.Api.Data;
using Rcm.Api.Features.Users;
using Rcm.Api.Tests.TestSupport;

namespace Rcm.Api.Tests.Features;

public class UsersTests
{
    [Fact]
    public async Task HandleAsync_SeededUsers_ReturnsRolesTopDownGroupedByTeam()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();

        var users = await new GetUsersHandler(db).HandleAsync(CancellationToken.None);

        Assert.Equal(
            ["Morgan", "Quinn", "Sam", "Taylor", "Jordan", "Casey", "Alex", "Blake", "Charlie", "Dana", "Emery"],
            users.Select(u => u.Name));
    }

    [Fact]
    public async Task HandleAsync_ManagerNotListed_PutsReportLastInRole()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();
        await db.Users.Where(u => u.Id == SeedData.Sam).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, false));

        var users = await new GetUsersHandler(db).HandleAsync(CancellationToken.None);

        Assert.Equal(
            ["Morgan", "Quinn", "Taylor", "Jordan", "Casey", "Charlie", "Dana", "Emery", "Alex", "Blake"],
            users.Select(u => u.Name));
    }

    [Fact]
    public async Task HandleAsync_InactiveUser_IsLeftOut()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();
        await db.Users.Where(u => u.Id == SeedData.Emery).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, false));

        var users = await new GetUsersHandler(db).HandleAsync(CancellationToken.None);

        Assert.Equal(10, users.Count);
        Assert.DoesNotContain(users, u => u.Id == SeedData.Emery);
    }
}
