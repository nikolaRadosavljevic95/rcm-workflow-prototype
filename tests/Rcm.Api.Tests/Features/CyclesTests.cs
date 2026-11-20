using Microsoft.EntityFrameworkCore;
using Rcm.Api.Auth;
using Rcm.Api.Data;
using Rcm.Api.Domain;
using Rcm.Api.Features;
using Rcm.Api.Features.Cycles;
using Rcm.Api.Features.WorkItems;
using Rcm.Api.Tests.TestSupport;

namespace Rcm.Api.Tests.Features;

// Seed on Nov 19: October closed, November open with 20 items, 9 of them Approved.
public class CyclesTests
{
    private static readonly CurrentUser Morgan = new(SeedData.Morgan, "Morgan", Role.Manager);
    private static readonly CurrentUser Sam = new(SeedData.Sam, "Sam", Role.Supervisor);
    private static readonly CurrentUser Alex = new(SeedData.Alex, "Alex", Role.Biller);
    private static readonly DateOnly December15 = new(2026, 12, 15);

    [Fact]
    public async Task Open_WhileMonthOpen_ReturnsInvalid()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();

        var result = await Open(db, database, Morgan, December15);

        Assert.Equal(ErrorKind.Invalid, result.Error?.Kind);
        Assert.Equal(2, await db.ReviewCycles.CountAsync());
    }

    [Fact]
    public async Task Open_Supervisor_ReturnsForbidden()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await CloseNovemberAsync(database);
        await using var db = database.CreateContext();

        var result = await Open(db, database, Sam, December15);

        Assert.Equal(ErrorKind.Forbidden, result.Error?.Kind);
    }

    [Fact]
    public async Task Open_DueDateOutsideMonth_ReturnsInvalid()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await CloseNovemberAsync(database);
        await using var db = database.CreateContext();

        var result = await Open(db, database, Morgan, new DateOnly(2026, 11, 30));

        Assert.Equal(ErrorKind.Invalid, result.Error?.Kind);
    }

    [Fact]
    public async Task Open_AfterClose_OneNotStartedItemPerFacilityForItsOwner()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await CloseNovemberAsync(database);
        await using var db = database.CreateContext();

        var result = await Open(db, database, Morgan, December15);

        Assert.Equal((2026, 12, CycleStatus.Open, December15), (result.Value.Year, result.Value.Month, result.Value.Status, result.Value.DueDate));
        var items = await db.WorkItems
            .AsNoTracking()
            .Include(w => w.Facility)
            .Include(w => w.PreviousItem)
            .Include(w => w.Events)
            .Where(w => w.CycleId == result.Value.Id)
            .ToListAsync();
        Assert.Equal(20, items.Count);
        Assert.All(items, item =>
        {
            Assert.Equal(item.Facility!.OwnerId, item.AssigneeId);
            Assert.Equal((WorkItemStatus.NotStarted, December15, 1), (item.Status, item.DueDate, item.Version));
            Assert.Null(item.ArTotalCents);
            Assert.Null(item.ArOver90Cents);
            Assert.Equal(item.FacilityId, item.PreviousItem?.FacilityId);
            var created = Assert.Single(item.Events);
            Assert.Equal((WorkItemEventType.Created, SeedData.Morgan), (created.Type, created.ActorId));
        });
    }

    [Fact]
    public async Task Open_InactiveFacility_GetsNoItem()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await CloseNovemberAsync(database);
        await using var db = database.CreateContext();
        var inactive = await db.Facilities.SingleAsync(f => f.Code == "F12");
        inactive.IsActive = false;
        await db.SaveChangesAsync();

        var result = await Open(db, database, Morgan, December15);

        var facilities = await db.WorkItems.Where(w => w.CycleId == result.Value.Id).Select(w => w.FacilityId).ToListAsync();
        Assert.Equal(19, facilities.Count);
        Assert.DoesNotContain(inactive.Id, facilities);
    }

    [Fact]
    public async Task Open_SameMonthOpenedConcurrently_ReturnsConflictAndSavesNothing()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await CloseNovemberAsync(database);
        await using var db = database.CreateContextRacing(other =>
        {
            other.ReviewCycles.Add(new ReviewCycle
            {
                Year = 2026,
                Month = 12,
                DueDate = December15,
                Status = CycleStatus.Open,
                OpenedById = SeedData.Quinn,
                OpenedAt = database.Calendar.UtcNow,
            });
            return Task.CompletedTask;
        });

        var result = await Open(db, database, Morgan, December15);

        Assert.Equal(ErrorKind.Conflict, result.Error?.Kind);
        await using var check = database.CreateContext();
        var december = await check.ReviewCycles.SingleAsync(c => c.Year == 2026 && c.Month == 12);
        Assert.Equal(SeedData.Quinn, december.OpenedById);
        Assert.False(await check.WorkItems.AnyAsync(w => w.CycleId == december.Id));
    }

    [Fact]
    public async Task ClosePreview_OpenMonth_ListsEveryItemNotApprovedIncludingSubmitted()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();
        var november = await NovemberIdAsync(db);

        var result = await new CloseCycleHandler(db, database.Calendar).PreviewAsync(Morgan, november, CancellationToken.None);

        Assert.Equal(
            ["F01", "F02", "F03", "F04", "F07", "F09", "F13", "F14", "F15", "F17", "F19"],
            result.Value.Items.Select(i => i.Facility.Code));
        var f04 = result.Value.Items.Single(i => i.Facility.Code == "F04");
        Assert.Equal((WorkItemStatus.Submitted, "Alex"), (f04.Status, f04.Owner.Name));
    }

    [Fact]
    public async Task ClosePreview_Supervisor_ReturnsForbidden()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();

        var result = await new CloseCycleHandler(db, database.Calendar)
            .PreviewAsync(Sam, await NovemberIdAsync(db), CancellationToken.None);

        Assert.Equal(ErrorKind.Forbidden, result.Error?.Kind);
    }

    [Fact]
    public async Task ClosePreview_ClosedMonth_ReturnsInvalid()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();
        var october = await db.ReviewCycles.Where(c => c.Month == 10).Select(c => c.Id).SingleAsync();

        var result = await new CloseCycleHandler(db, database.Calendar).PreviewAsync(Morgan, october, CancellationToken.None);

        Assert.Equal(ErrorKind.Invalid, result.Error?.Kind);
    }

    [Fact]
    public async Task Close_MissingMonth_ReturnsNotFound()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();

        var result = await new CloseCycleHandler(db, database.Calendar).CloseAsync(Morgan, 999, CancellationToken.None);

        Assert.Equal(ErrorKind.NotFound, result.Error?.Kind);
    }

    [Fact]
    public async Task Close_WithSubmittedItems_TheyBecomeIncompleteAndApprovedStay()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();
        var november = await NovemberIdAsync(db);
        var before = await db.WorkItems.AsNoTracking().Where(w => w.CycleId == november).ToDictionaryAsync(w => w.Id);

        var result = await new CloseCycleHandler(db, database.Calendar).CloseAsync(Morgan, november, CancellationToken.None);

        Assert.Equal(CycleStatus.Closed, result.Value.Status);
        await using var check = database.CreateContext();
        var cycle = await check.ReviewCycles.SingleAsync(c => c.Id == november);
        Assert.Equal((SeedData.Morgan, database.Calendar.UtcNow), (cycle.ClosedById, cycle.ClosedAt));
        var after = await check.WorkItems.Include(w => w.Events).Where(w => w.CycleId == november).ToListAsync();
        Assert.Equal(11, after.Count(w => w.Status == WorkItemStatus.Incomplete));
        Assert.Equal(9, after.Count(w => w.Status == WorkItemStatus.Approved));
        foreach (var item in after)
        {
            var old = before[item.Id];
            var closing = item.Events.Where(e => e.ToStatus == WorkItemStatus.Incomplete).ToList();
            if (old.Status == WorkItemStatus.Approved)
            {
                Assert.Equal(old.Version, item.Version);
                Assert.Empty(closing);
            }
            else
            {
                Assert.Equal(old.Version + 1, item.Version);
                Assert.Equal(old.Status, Assert.Single(closing).FromStatus);
            }
        }
    }

    [Fact]
    public async Task Close_ThenTransition_ReturnsInvalid()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await CloseNovemberAsync(database);
        await using var db = database.CreateContext();
        var item = await db.WorkItems.AsNoTracking().SingleAsync(w => w.Cycle!.Month == 11 && w.Facility!.Code == "F01");

        var result = await new TransitionWorkItemHandler(db, new WorkItemReader(db), Contexts(db, database), database.Calendar)
            .HandleAsync(Alex, item.Id, new TransitionRequest(WorkflowAction.Submit, "Done", item.Version), CancellationToken.None);

        Assert.Equal(ErrorKind.Invalid, result.Error?.Kind);
    }

    [Fact]
    public async Task Close_ItemChangedConcurrently_ReturnsConflictAndSavesNothing()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        int november;
        await using (var setup = database.CreateContext())
        {
            november = await NovemberIdAsync(setup);
        }

        await using var db = database.CreateContextRacing(async other =>
        {
            var item = await other.WorkItems.SingleAsync(w => w.CycleId == november && w.Facility!.Code == "F01");
            item.Version++;
        });

        var result = await new CloseCycleHandler(db, database.Calendar).CloseAsync(Morgan, november, CancellationToken.None);

        Assert.Equal(ErrorKind.Conflict, result.Error?.Kind);
        await using var check = database.CreateContext();
        Assert.Equal(CycleStatus.Open, (await check.ReviewCycles.SingleAsync(c => c.Id == november)).Status);
        Assert.False(await check.WorkItems.AnyAsync(w => w.Status == WorkItemStatus.Incomplete && w.CycleId == november));
    }

    [Fact]
    public async Task OpenAfterClose_UnapprovedLastMonth_ShowsMissedLastMonth()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await CloseNovemberAsync(database);
        await using var db = database.CreateContext();
        var december = await Open(db, database, Morgan, December15);

        var overview = await new GetOverviewHandler(new WorkItemReader(db), Contexts(db, database))
            .HandleAsync(Morgan, december.Value.Id, CancellationToken.None);

        var missed = overview.Value.Items
            .Where(r => r.Flags.Contains(ItemFlag.MissedLastMonth))
            .Select(r => r.Facility.Code)
            .Order(StringComparer.Ordinal);
        Assert.Equal(["F01", "F02", "F03", "F04", "F07", "F09", "F13", "F14", "F15", "F17", "F19"], missed);
    }

    [Fact]
    public async Task Overview_ManagerOnOpenMonth_CanCloseOnly()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();

        var overview = await new GetOverviewHandler(new WorkItemReader(db), Contexts(db, database))
            .HandleAsync(Morgan, null, CancellationToken.None);

        Assert.Equal(new MonthActionsResponse(true, null), overview.Value.MonthActions);
    }

    [Fact]
    public async Task Overview_ManagerAfterClose_OffersDecemberDueThe15th()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await CloseNovemberAsync(database);
        await using var db = database.CreateContext();

        var overview = await new GetOverviewHandler(new WorkItemReader(db), Contexts(db, database))
            .HandleAsync(Morgan, null, CancellationToken.None);

        Assert.Equal(new MonthActionsResponse(false, new NextMonthResponse(2026, 12, December15)), overview.Value.MonthActions);
    }

    [Fact]
    public async Task Overview_Supervisor_NoMonthActions()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();

        var overview = await new GetOverviewHandler(new WorkItemReader(db), Contexts(db, database))
            .HandleAsync(Sam, null, CancellationToken.None);

        Assert.Equal(new MonthActionsResponse(false, null), overview.Value.MonthActions);
    }

    private static Task<Result<CycleResponse>> Open(
        AppDbContext db, SqliteTestDatabase database, CurrentUser user, DateOnly dueDate) =>
        new OpenCycleHandler(db, database.Calendar).HandleAsync(user, new OpenCycleRequest(dueDate), CancellationToken.None);

    private static async Task CloseNovemberAsync(SqliteTestDatabase database)
    {
        await using var db = database.CreateContext();
        var result = await new CloseCycleHandler(db, database.Calendar)
            .CloseAsync(Morgan, await NovemberIdAsync(db), CancellationToken.None);
        Assert.True(result.IsSuccess);
    }

    private static Task<int> NovemberIdAsync(AppDbContext db) =>
        db.ReviewCycles.Where(c => c.Year == 2026 && c.Month == 11).Select(c => c.Id).SingleAsync();

    private static RequestContextLoader Contexts(AppDbContext db, SqliteTestDatabase database) =>
        new(db, database.Calendar);
}
