using Microsoft.EntityFrameworkCore;
using Rcm.Api.Auth;
using Rcm.Api.Data;
using Rcm.Api.Domain;
using Rcm.Api.Features;
using Rcm.Api.Features.Cycles;
using Rcm.Api.Features.Facilities;
using Rcm.Api.Features.WorkItems;
using Rcm.Api.Tests.TestSupport;

namespace Rcm.Api.Tests.Features;

// Seed on Nov 19: Alex owns F01 to F04 (F04 Submitted, reviewed by Sam), Blake owns F05 to F08 (F05 Approved).
// Dana is away today with no cover, and Jordan reviews her items.
public class FacilitiesTests
{
    private const string Reason = "Workload balance";
    private static readonly CurrentUser Morgan = new(SeedData.Morgan, "Morgan", Role.Manager);
    private static readonly CurrentUser Quinn = new(SeedData.Quinn, "Quinn", Role.Manager);
    private static readonly CurrentUser Sam = new(SeedData.Sam, "Sam", Role.Supervisor);
    private static readonly CurrentUser Jordan = new(SeedData.Jordan, "Jordan", Role.Supervisor);
    private static readonly CurrentUser Alex = new(SeedData.Alex, "Alex", Role.Biller);

    [Fact]
    public async Task Get_Manager_ListsFacilitiesWithOwnersAndEligibleOwners()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();

        var result = await new GetFacilitiesHandler(db).HandleAsync(Morgan, CancellationToken.None);

        Assert.Equal(20, result.Value.Facilities.Count);
        Assert.Equal(("F01", "Alex", true), Summary(result.Value.Facilities[0]));
        Assert.Equal(("F20", "Casey", true), Summary(result.Value.Facilities[19]));
        Assert.Equal(
            ["Alex", "Blake", "Casey", "Charlie", "Dana", "Emery", "Jordan", "Sam", "Taylor"],
            result.Value.EligibleOwners.Select(p => p.Name));
    }

    [Fact]
    public async Task Get_Supervisor_ListsFacilitiesWithoutEligibleOwners()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();

        var result = await new GetFacilitiesHandler(db).HandleAsync(Sam, CancellationToken.None);

        Assert.Equal(20, result.Value.Facilities.Count);
        Assert.Empty(result.Value.EligibleOwners);
    }

    [Fact]
    public async Task Get_Biller_ReturnsForbidden()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();

        var result = await new GetFacilitiesHandler(db).HandleAsync(Alex, CancellationToken.None);

        Assert.Equal(ErrorKind.Forbidden, result.Error?.Kind);
    }

    [Fact]
    public async Task Move_Supervisor_ReturnsForbidden()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();

        var result = await Move(db, database, Sam, await FacilityIdAsync(db, "F01"), SeedData.Dana);

        Assert.Equal(ErrorKind.Forbidden, result.Error?.Kind);
    }

    [Fact]
    public async Task Move_MissingFacility_ReturnsNotFound()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();

        var result = await Move(db, database, Morgan, 999, SeedData.Dana);

        Assert.Equal(ErrorKind.NotFound, result.Error?.Kind);
    }

    [Fact]
    public async Task Move_InvalidRequest_ReturnsInvalidAndSavesNothing()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();
        var f01 = await FacilityIdAsync(db, "F01");

        var result = await Move(db, database, Morgan, f01, SeedData.Alex);

        Assert.Equal(ErrorKind.Invalid, result.Error?.Kind);
        await using var check = database.CreateContext();
        Assert.Equal(1, await check.FacilityAssignments.CountAsync(a => a.FacilityId == f01));
    }

    [Fact]
    public async Task Move_WithOpenItem_ItemMovesAndHistoryShowsTheMove()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();
        var f01 = await FacilityIdAsync(db, "F01");
        var before = await OpenItemAsync(db, f01);

        var result = await Move(db, database, Morgan, f01, SeedData.Dana);

        Assert.Equal(("F01", "Dana", true), Summary(result.Value.Facility));
        Assert.Equal(before.Id, result.Value.MovedItemId);
        await using var check = database.CreateContext();
        var item = await check.WorkItems.Include(w => w.Events).SingleAsync(w => w.Id == before.Id);
        Assert.Equal((SeedData.Dana, before.Status, before.Version + 1), (item.AssigneeId, item.Status, item.Version));
        var moved = item.Events.Single(e => e.Type == WorkItemEventType.Reassigned);
        Assert.Equal((SeedData.Alex, SeedData.Dana, SeedData.Morgan, Reason), (moved.FromUserId, moved.ToUserId, moved.ActorId, moved.Text));
    }

    [Fact]
    public async Task Move_AssignmentHistory_EndsOldPeriodAndStartsNewAtSameTime()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();
        var f01 = await FacilityIdAsync(db, "F01");

        await Move(db, database, Morgan, f01, SeedData.Dana);

        await using var check = database.CreateContext();
        var periods = await check.FacilityAssignments.Where(a => a.FacilityId == f01).OrderBy(a => a.Id).ToListAsync();
        Assert.Equal(2, periods.Count);
        var now = database.Calendar.UtcNow;
        Assert.Equal((SeedData.Alex, (DateTime?)now), (periods[0].UserId, periods[0].EndedAt));
        Assert.Equal((SeedData.Dana, now, (DateTime?)null, Reason, SeedData.Morgan), (periods[1].UserId, periods[1].StartedAt, periods[1].EndedAt, periods[1].Reason, periods[1].ChangedById));
    }

    [Fact]
    public async Task Move_OpenItemOff_OldOwnerFinishesMonthAndNextMonthGoesToNewOwner()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();
        var f01 = await FacilityIdAsync(db, "F01");
        var november = await OpenItemAsync(db, f01);

        var result = await Move(db, database, Morgan, f01, SeedData.Dana, moveOpenItem: false);

        Assert.Null(result.Value.MovedItemId);
        await using var check = database.CreateContext();
        var item = await check.WorkItems.Include(w => w.Events).SingleAsync(w => w.Id == november.Id);
        Assert.Equal((SeedData.Alex, november.Version), (item.AssigneeId, item.Version));
        Assert.DoesNotContain(item.Events, e => e.Type == WorkItemEventType.Reassigned);

        var cycles = new CloseCycleHandler(check, database.Calendar);
        Assert.True((await cycles.CloseAsync(Morgan, item.CycleId, CancellationToken.None)).IsSuccess);
        var december = await new OpenCycleHandler(check, database.Calendar)
            .HandleAsync(Morgan, new OpenCycleRequest(new DateOnly(2026, 12, 15)), CancellationToken.None);
        var next = await check.WorkItems.SingleAsync(w => w.CycleId == december.Value.Id && w.FacilityId == f01);
        Assert.Equal(SeedData.Dana, next.AssigneeId);
    }

    [Fact]
    public async Task Move_WithSubmittedItem_KeepsSubmitterAndNewOwnersReviewerReviews()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();
        var f04 = await FacilityIdAsync(db, "F04");
        Assert.Contains("F04", await WaitingForReviewAsync(database, Sam));

        await Move(db, database, Morgan, f04, SeedData.Dana);

        await using var check = database.CreateContext();
        var item = await OpenItemAsync(check, f04);
        Assert.Equal((WorkItemStatus.Submitted, SeedData.Alex), (item.Status, item.SubmittedById));
        Assert.DoesNotContain("F04", await WaitingForReviewAsync(database, Sam));
        Assert.Contains("F04", await WaitingForReviewAsync(database, Jordan));
    }

    [Fact]
    public async Task Move_NewOwnerAwayWithoutCover_MovedItemShowsNeedsCover()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();
        var f01 = await FacilityIdAsync(db, "F01");

        await Move(db, database, Morgan, f01, SeedData.Dana);

        await using var check = database.CreateContext();
        var overview = await new GetOverviewHandler(new WorkItemReader(check), new RequestContextLoader(check, database.Calendar))
            .HandleAsync(Morgan, null, CancellationToken.None);
        Assert.Contains(ItemFlag.NeedsCover, overview.Value.Items.Single(r => r.Facility.Code == "F01").Flags);
    }

    [Fact]
    public async Task Move_ApprovedItem_NeverMoves()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();
        var f05 = await FacilityIdAsync(db, "F05");
        var before = await OpenItemAsync(db, f05);

        var result = await Move(db, database, Morgan, f05, SeedData.Dana);

        Assert.Equal("Dana", result.Value.Facility.Owner.Name);
        Assert.Null(result.Value.MovedItemId);
        await using var check = database.CreateContext();
        var item = await OpenItemAsync(check, f05);
        Assert.Equal((WorkItemStatus.Approved, SeedData.Blake, before.Version), (item.Status, item.AssigneeId, item.Version));
    }

    [Fact]
    public async Task Move_IncompleteItem_NeverMoves()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();
        var f07 = await FacilityIdAsync(db, "F07");
        var seeded = await db.WorkItems.SingleAsync(w => w.FacilityId == f07 && w.Cycle!.Status == CycleStatus.Open);
        seeded.Status = WorkItemStatus.Incomplete;
        await db.SaveChangesAsync();

        var result = await Move(db, database, Morgan, f07, SeedData.Dana);

        Assert.Null(result.Value.MovedItemId);
        await using var check = database.CreateContext();
        Assert.Equal(SeedData.Blake, (await OpenItemAsync(check, f07)).AssigneeId);
    }

    [Fact]
    public async Task Move_ItemChangedConcurrently_ReturnsConflictAndSavesNothing()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        int f01;
        await using (var setup = database.CreateContext())
        {
            f01 = await FacilityIdAsync(setup, "F01");
        }

        await using var db = database.CreateContextRacing(async other =>
        {
            var item = await other.WorkItems.SingleAsync(w => w.FacilityId == f01 && w.Cycle!.Status == CycleStatus.Open);
            item.Version++;
        });

        var result = await Move(db, database, Morgan, f01, SeedData.Dana);

        Assert.Equal(ErrorKind.Conflict, result.Error?.Kind);
        await using var check = database.CreateContext();
        Assert.Equal(SeedData.Alex, (await check.Facilities.SingleAsync(f => f.Id == f01)).OwnerId);
        Assert.Equal(1, await check.FacilityAssignments.CountAsync(a => a.FacilityId == f01));
        Assert.Equal(SeedData.Alex, (await OpenItemAsync(check, f01)).AssigneeId);
    }

    [Fact]
    public async Task Move_SameFacilityMovedConcurrently_ReturnsConflictAndKeepsOneCurrentOwner()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        int f07;
        await using (var setup = database.CreateContext())
        {
            f07 = await FacilityIdAsync(setup, "F07");
        }

        // Quinn's move saves first. Neither move takes the item, so only the assignment index can catch the clash.
        await using var db = database.CreateContextRacing(async other =>
        {
            var first = await new MoveFacilityHandler(other, database.Calendar).HandleAsync(
                Quinn, f07, new MoveFacilityRequest(SeedData.Alex, Reason, MoveOpenItem: false), CancellationToken.None);
            Assert.True(first.IsSuccess);
        });

        var result = await Move(db, database, Morgan, f07, SeedData.Dana, moveOpenItem: false);

        Assert.Equal(ErrorKind.Conflict, result.Error?.Kind);
        await using var check = database.CreateContext();
        Assert.Equal(SeedData.Alex, (await check.Facilities.SingleAsync(f => f.Id == f07)).OwnerId);
        var periods = await check.FacilityAssignments.Where(a => a.FacilityId == f07).ToListAsync();
        Assert.Equal(2, periods.Count);
        Assert.Equal(SeedData.Alex, Assert.Single(periods, a => a.EndedAt == null).UserId);
    }

    [Fact]
    public async Task Overview_Manager_CanMoveFacilities()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();
        var handler = new GetOverviewHandler(new WorkItemReader(db), new RequestContextLoader(db, database.Calendar));

        Assert.True((await handler.HandleAsync(Morgan, null, CancellationToken.None)).Value.CanMoveFacilities);
        Assert.False((await handler.HandleAsync(Sam, null, CancellationToken.None)).Value.CanMoveFacilities);
    }

    [Fact]
    public async Task Overview_ManagerOnClosedMonth_CannotMoveFacilities()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();
        var handler = new GetOverviewHandler(new WorkItemReader(db), new RequestContextLoader(db, database.Calendar));
        var closedId = await db.ReviewCycles.Where(c => c.Status == CycleStatus.Closed).Select(c => c.Id).SingleAsync();

        Assert.False((await handler.HandleAsync(Morgan, closedId, CancellationToken.None)).Value.CanMoveFacilities);
    }

    private static Task<Result<MoveFacilityResponse>> Move(
        AppDbContext db, SqliteTestDatabase database, CurrentUser user, int facilityId, int newOwnerId, bool moveOpenItem = true) =>
        new MoveFacilityHandler(db, database.Calendar)
            .HandleAsync(user, facilityId, new MoveFacilityRequest(newOwnerId, Reason, moveOpenItem), CancellationToken.None);

    private static async Task<List<string>> WaitingForReviewAsync(SqliteTestDatabase database, CurrentUser user)
    {
        await using var db = database.CreateContext();
        var overview = await new GetOverviewHandler(new WorkItemReader(db), new RequestContextLoader(db, database.Calendar))
            .HandleAsync(user, null, CancellationToken.None);
        return overview.Value.WaitingForMyReview.Select(r => r.Facility.Code).ToList();
    }

    private static (string Code, string Owner, bool IsActive) Summary(FacilityRow row) =>
        (row.Code, row.Owner.Name, row.IsActive);

    private static Task<int> FacilityIdAsync(AppDbContext db, string code) =>
        db.Facilities.Where(f => f.Code == code).Select(f => f.Id).SingleAsync();

    private static Task<WorkItem> OpenItemAsync(AppDbContext db, int facilityId) =>
        db.WorkItems.AsNoTracking().SingleAsync(w => w.FacilityId == facilityId && w.Cycle!.Status == CycleStatus.Open);
}
