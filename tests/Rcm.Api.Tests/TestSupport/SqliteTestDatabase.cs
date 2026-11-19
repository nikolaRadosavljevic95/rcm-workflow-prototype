using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Rcm.Api.Data;
using Rcm.Api.Domain;

namespace Rcm.Api.Tests.TestSupport;

// SQLite in-memory lives as long as its connection, so the connection stays open for the test.
public sealed class SqliteTestDatabase : IAsyncDisposable
{
    public static readonly TimeZoneInfo NewYork = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

    // Thursday 2026-11-19, 10:00 in New York.
    public static readonly DateTimeOffset MidMonth = new(2026, 11, 19, 15, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection connection;
    private readonly DbContextOptions<AppDbContext> options;

    private SqliteTestDatabase(DateTimeOffset utcNow)
    {
        connection = new SqliteConnection("DataSource=:memory:");
        options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        Calendar = new BusinessCalendar(new FixedTimeProvider(utcNow), NewYork);
    }

    public BusinessCalendar Calendar { get; }

    public static async Task<SqliteTestDatabase> CreateAsync(DateTimeOffset utcNow)
    {
        var database = new SqliteTestDatabase(utcNow);
        await database.connection.OpenAsync();
        await using var db = database.CreateContext();
        await db.Database.EnsureCreatedAsync();
        return database;
    }

    public static async Task<SqliteTestDatabase> CreateSeededAsync(DateTimeOffset utcNow)
    {
        var database = await CreateAsync(utcNow);
        await using var db = database.CreateContext();
        await SeedData.SeedAsync(db, database.Calendar);
        return database;
    }

    public AppDbContext CreateContext() => new(options);

    public async ValueTask DisposeAsync() => await connection.DisposeAsync();
}
