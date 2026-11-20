using Microsoft.EntityFrameworkCore;
using Rcm.Api.Auth;
using Rcm.Api.Data;
using Rcm.Api.Domain;
using Rcm.Api.Features;
using Rcm.Api.Features.Absences;
using Rcm.Api.Features.WorkItems;
using Rcm.Api.Tests.TestSupport;

namespace Rcm.Api.Tests.Features;

// Seed on Nov 19: Blake away Nov 17 to 23 covered by Alex, Dana Nov 19 to 21 with no cover, Taylor Nov 18 to 22 covered by Sam.
public class AbsencesTests
{
    private static readonly DateOnly Today = new(2026, 11, 19);
    private static readonly CurrentUser Morgan = new(SeedData.Morgan, "Morgan", Role.Manager);
    private static readonly CurrentUser Sam = new(SeedData.Sam, "Sam", Role.Supervisor);
    private static readonly CurrentUser Jordan = new(SeedData.Jordan, "Jordan", Role.Supervisor);
    private static readonly CurrentUser Casey = new(SeedData.Casey, "Casey", Role.Supervisor);
    private static readonly CurrentUser Alex = new(SeedData.Alex, "Alex", Role.Biller);

    [Fact]
    public async Task Get_Biller_ReturnsForbidden()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();

        var result = await new GetAbsencesHandler(db, Contexts(db, database)).HandleAsync(Alex, CancellationToken.None);

        Assert.Equal(ErrorKind.Forbidden, result.Error?.Kind);
    }

    [Fact]
    public async Task Get_Supervisor_SeesTeamAndPeopleToRecordFor()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();

        var response = (await new GetAbsencesHandler(db, Contexts(db, database)).HandleAsync(Jordan, CancellationToken.None)).Value;

        var dana = Assert.Single(response.Absences);
        Assert.Equal(("Dana", AbsenceToday.AwayNeedsCover, true, false), (dana.Person.Name, dana.Today, dana.CanEdit, dana.CanDelete));
        Assert.Equal(["Dana", "Jordan"], response.People.Select(p => p.Name));
    }

    [Fact]
    public async Task Get_SupervisorWhoCovers_SeesThatAbsenceReadOnly()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();

        var response = (await new GetAbsencesHandler(db, Contexts(db, database)).HandleAsync(Sam, CancellationToken.None)).Value;

        Assert.Equal(
            [("Blake", true, AbsenceToday.AwayCovered), ("Taylor", false, AbsenceToday.AwayCovered)],
            response.Absences.Select(a => (a.Person.Name, a.CanEdit, a.Today)));
    }

    [Fact]
    public async Task Get_Manager_SeesEveryCurrentAbsence()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();

        var response = (await new GetAbsencesHandler(db, Contexts(db, database)).HandleAsync(Morgan, CancellationToken.None)).Value;

        Assert.Equal(["Blake", "Taylor", "Dana"], response.Absences.Select(a => a.Person.Name));
        Assert.All(response.Absences, a => Assert.True(a.CanEdit));
        Assert.Equal(11, response.People.Count);
    }

    [Fact]
    public async Task Create_InScope_SavesAndReturnsRow()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();

        var result = await Create(db, database, Jordan, SeedData.Jordan, 5, 6, SeedData.Casey, "Training");

        Assert.Equal(("Jordan", "Casey", AbsenceToday.Upcoming, true), (result.Value.Person.Name, result.Value.Cover?.Name, result.Value.Today, result.Value.CanDelete));
        var stored = await db.Absences.AsNoTracking().SingleAsync(a => a.Id == result.Value.Id);
        Assert.Equal((SeedData.Jordan, "Training"), (stored.CreatedById, stored.Note));
    }

    [Fact]
    public async Task Create_OutsideTeam_ReturnsForbidden()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();

        var result = await Create(db, database, Jordan, SeedData.Alex, 5, 6, null);

        Assert.Equal(ErrorKind.Forbidden, result.Error?.Kind);
    }

    [Fact]
    public async Task Create_BillerAsCoverForSupervisor_ReturnsInvalid()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();

        var result = await Create(db, database, Morgan, SeedData.Casey, 5, 6, SeedData.Emery);

        Assert.Equal(ErrorKind.Invalid, result.Error?.Kind);
    }

    [Fact]
    public async Task Create_Overlapping_ReturnsInvalid()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();

        var result = await Create(db, database, Jordan, SeedData.Dana, 2, 4, null);

        Assert.Equal("Dana is already away from Nov 19 to Nov 21.", result.Error?.Message);
    }

    [Fact]
    public async Task Create_SelfAsCover_ReturnsInvalid()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();

        var result = await Create(db, database, Jordan, SeedData.Jordan, 5, 6, SeedData.Jordan);

        Assert.Equal(ErrorKind.Invalid, result.Error?.Kind);
    }

    [Fact]
    public async Task Create_InactiveCover_ReturnsInvalid()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();
        await db.Users.Where(u => u.Id == SeedData.Emery).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, false));

        var result = await Create(db, database, Jordan, SeedData.Dana, 5, 6, SeedData.Emery);

        Assert.Equal("Emery is inactive.", result.Error?.Message);
    }

    [Fact]
    public async Task Create_CoverAway_NamesTheDay()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();

        var result = await Create(db, database, Jordan, SeedData.Dana, 3, 6, SeedData.Blake);

        Assert.Equal("Blake is away on Nov 22.", result.Error?.Message);
    }

    [Fact]
    public async Task Update_GiveDanaACover_ClearsNeedsCover()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();
        var dana = await db.Absences.AsNoTracking().SingleAsync(a => a.UserId == SeedData.Dana);

        var updated = await new UpdateAbsenceHandler(db, Contexts(db, database)).HandleAsync(
            Jordan, dana.Id, new UpdateAbsenceRequest(dana.StartDate, dana.EndDate, SeedData.Jordan, dana.Note), CancellationToken.None);

        Assert.Equal(AbsenceToday.AwayCovered, updated.Value.Today);
        var overview = (await new GetOverviewHandler(new WorkItemReader(db), Contexts(db, database))
            .HandleAsync(Jordan, null, CancellationToken.None)).Value;
        Assert.All(overview.Items, r => Assert.DoesNotContain(ItemFlag.NeedsCover, r.Flags));
        Assert.All(overview.Items, r => Assert.Equal("Dana", r.CoveringFor?.Name));
    }

    [Fact]
    public async Task Update_StartedAbsenceStartMoved_ReturnsInvalid()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();
        var blake = await db.Absences.AsNoTracking().SingleAsync(a => a.UserId == SeedData.Blake);

        var result = await new UpdateAbsenceHandler(db, Contexts(db, database)).HandleAsync(
            Sam, blake.Id, new UpdateAbsenceRequest(Today, blake.EndDate, blake.CoverUserId, null), CancellationToken.None);

        Assert.Equal(ErrorKind.Invalid, result.Error?.Kind);
    }

    [Fact]
    public async Task Update_ReadOnlyCoverOrOtherTeam_ReturnsForbiddenOrNotFound()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();
        var taylor = await db.Absences.AsNoTracking().SingleAsync(a => a.UserId == SeedData.Taylor);
        var request = new UpdateAbsenceRequest(taylor.StartDate, taylor.EndDate, null, null);
        var handler = new UpdateAbsenceHandler(db, Contexts(db, database));

        Assert.Equal(ErrorKind.Forbidden, (await handler.HandleAsync(Sam, taylor.Id, request, CancellationToken.None)).Error?.Kind);
        Assert.Equal(ErrorKind.NotFound, (await handler.HandleAsync(Casey, taylor.Id, request, CancellationToken.None)).Error?.Kind);
    }

    [Fact]
    public async Task Delete_StartedAbsence_ReturnsInvalid()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();
        var blake = await db.Absences.AsNoTracking().SingleAsync(a => a.UserId == SeedData.Blake);

        var error = await new DeleteAbsenceHandler(db, Contexts(db, database)).HandleAsync(Sam, blake.Id, CancellationToken.None);

        Assert.Equal(ErrorKind.Invalid, error?.Kind);
        Assert.True(await db.Absences.AnyAsync(a => a.Id == blake.Id));
    }

    [Fact]
    public async Task Delete_UpcomingAbsence_RemovesIt()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();
        var created = await Create(db, database, Jordan, SeedData.Jordan, 5, 6, null);

        var error = await new DeleteAbsenceHandler(db, Contexts(db, database)).HandleAsync(Jordan, created.Value.Id, CancellationToken.None);

        Assert.Null(error);
        Assert.False(await db.Absences.AnyAsync(a => a.Id == created.Value.Id));
    }

    private static RequestContextLoader Contexts(AppDbContext db, SqliteTestDatabase database) => new(db, database.Calendar);

    private static Task<Result<AbsenceRow>> Create(
        AppDbContext db, SqliteTestDatabase database, CurrentUser user, int personId, int fromDays, int toDays, int? coverId, string? note = null) =>
        new CreateAbsenceHandler(db, Contexts(db, database), database.Calendar).HandleAsync(
            user,
            new CreateAbsenceRequest(personId, Today.AddDays(fromDays), Today.AddDays(toDays), coverId, note),
            CancellationToken.None);
}
