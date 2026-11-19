using Microsoft.EntityFrameworkCore;
using Rcm.Api.Data;
using Rcm.Api.Domain;
using Rcm.Api.Tests.TestSupport;

namespace Rcm.Api.Tests.Data;

public class SeedDataTests
{
    private static readonly DateTimeOffset FirstOfMonthBeforeOpening = new(2026, 12, 1, 5, 30, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset FirstOfMonthAfterOpening = new(2026, 12, 1, 14, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset SecondOfMonth = new(2026, 12, 2, 15, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset January = new(2027, 1, 10, 15, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset AfterDaylightSaving = new(2026, 3, 9, 14, 0, 0, TimeSpan.Zero);

    public static TheoryData<DateTimeOffset> SeedTimes =>
    [
        SqliteTestDatabase.MidMonth,
        FirstOfMonthBeforeOpening,
        FirstOfMonthAfterOpening,
        SecondOfMonth,
        January,
        AfterDaylightSaving,
    ];

    public static TheoryData<DateTimeOffset> FirstOfMonthTimes => [FirstOfMonthBeforeOpening, FirstOfMonthAfterOpening];

    [Fact]
    public async Task SeedAsync_EmptyDatabase_CreatesUsersWithReportingLines()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();

        var users = await db.Users.Include(u => u.ReportsTo).ToListAsync();

        var expected = new Dictionary<string, (Role Role, string? ReportsTo)>
        {
            ["Morgan"] = (Role.Manager, null),
            ["Quinn"] = (Role.Manager, null),
            ["Sam"] = (Role.Supervisor, "Morgan"),
            ["Taylor"] = (Role.Supervisor, "Morgan"),
            ["Jordan"] = (Role.Supervisor, "Quinn"),
            ["Casey"] = (Role.Supervisor, "Quinn"),
            ["Alex"] = (Role.Biller, "Sam"),
            ["Blake"] = (Role.Biller, "Sam"),
            ["Charlie"] = (Role.Biller, "Taylor"),
            ["Dana"] = (Role.Biller, "Jordan"),
            ["Emery"] = (Role.Biller, "Casey"),
        };
        Assert.Equal(expected.Count, users.Count);
        Assert.All(users, u =>
        {
            Assert.Equal(expected[u.Name], (u.Role, u.ReportsTo?.Name));
            Assert.True(u.IsActive);
            Assert.EndsWith("@example.com", u.Email);
        });
    }

    [Fact]
    public async Task SeedAsync_EmptyDatabase_AssignsFacilitiesToOwners()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();

        var facilities = await db.Facilities.Include(f => f.Owner).OrderBy(f => f.Code).ToListAsync();
        var assignments = await db.FacilityAssignments.ToListAsync();

        string[] expectedOwners =
        [
            "Alex", "Alex", "Alex", "Alex", "Blake", "Blake", "Blake", "Blake", "Charlie", "Charlie",
            "Charlie", "Charlie", "Dana", "Dana", "Dana", "Emery", "Emery", "Emery", "Casey", "Casey",
        ];
        Assert.Equal(expectedOwners, facilities.Select(f => f.Owner!.Name));
        Assert.Equal(Enumerable.Range(1, 20).Select(n => $"F{n:00}"), facilities.Select(f => f.Code));
        Assert.Equal(Enumerable.Range(1, 20).Select(n => $"Facility {n:00}"), facilities.Select(f => f.Name));
        Assert.All(facilities, f =>
        {
            var assignment = Assert.Single(assignments, a => a.FacilityId == f.Id);
            Assert.Equal(f.OwnerId, assignment.UserId);
            Assert.Null(assignment.EndedAt);
        });
    }

    [Fact]
    public async Task SeedAsync_MidMonth_LastMonthClosedWithF07AndF14Incomplete()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();

        var cycle = await db.ReviewCycles.SingleAsync(c => c.Status == CycleStatus.Closed);
        var items = await db.WorkItems.Include(w => w.Facility).Where(w => w.CycleId == cycle.Id).ToListAsync();

        Assert.Equal((2026, 10), (cycle.Year, cycle.Month));
        Assert.Equal(new DateOnly(2026, 10, 15), cycle.DueDate);
        Assert.NotNull(cycle.ClosedAt);
        Assert.Equal(20, items.Count);
        Assert.Equal(["F07", "F14"], items.Where(w => w.Status == WorkItemStatus.Incomplete).Select(w => w.Facility!.Code).Order());
        Assert.Equal(18, items.Count(w => w.Status == WorkItemStatus.Approved));
    }

    [Fact]
    public async Task SeedAsync_MidMonth_CurrentMonthOpenAndDueYesterday()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();

        var cycle = await db.ReviewCycles.SingleAsync(c => c.Status == CycleStatus.Open);
        var dueDates = await db.WorkItems.Where(w => w.CycleId == cycle.Id).Select(w => w.DueDate).Distinct().ToListAsync();

        Assert.Equal((2026, 11), (cycle.Year, cycle.Month));
        Assert.Equal(new DateOnly(2026, 11, 18), cycle.DueDate);
        Assert.Equal([cycle.DueDate], dueDates);
        Assert.Null(cycle.ClosedAt);
    }

    [Fact]
    public async Task SeedAsync_FirstOfMonth_DueDateIsTheFirst()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(FirstOfMonthAfterOpening);
        await using var db = database.CreateContext();

        var cycle = await db.ReviewCycles.SingleAsync(c => c.Status == CycleStatus.Open);

        Assert.Equal(new DateOnly(2026, 12, 1), cycle.DueDate);
    }

    [Fact]
    public async Task SeedAsync_January_LastMonthIsDecemberOfPreviousYear()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(January);
        await using var db = database.CreateContext();

        var cycles = await db.ReviewCycles.OrderBy(c => c.Id).ToListAsync();

        Assert.Equal([(2026, 12, CycleStatus.Closed), (2027, 1, CycleStatus.Open)], cycles.Select(c => (c.Year, c.Month, c.Status)));
    }

    [Fact]
    public async Task SeedAsync_MidMonth_CurrentStatusesMatchDemoPlan()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();

        var statuses = await db.WorkItems
            .Where(w => w.Cycle!.Status == CycleStatus.Open)
            .Select(w => new { w.Facility!.Code, w.Status })
            .ToDictionaryAsync(w => w.Code, w => w.Status);

        var expected = new Dictionary<string, WorkItemStatus>
        {
            ["F01"] = WorkItemStatus.InProgress,
            ["F02"] = WorkItemStatus.Blocked,
            ["F03"] = WorkItemStatus.Returned,
            ["F04"] = WorkItemStatus.Submitted,
            ["F05"] = WorkItemStatus.Approved,
            ["F06"] = WorkItemStatus.Approved,
            ["F07"] = WorkItemStatus.NotStarted,
            ["F08"] = WorkItemStatus.Approved,
            ["F09"] = WorkItemStatus.Submitted,
            ["F10"] = WorkItemStatus.Approved,
            ["F11"] = WorkItemStatus.Approved,
            ["F12"] = WorkItemStatus.Approved,
            ["F13"] = WorkItemStatus.Blocked,
            ["F14"] = WorkItemStatus.NotStarted,
            ["F15"] = WorkItemStatus.InProgress,
            ["F16"] = WorkItemStatus.Approved,
            ["F17"] = WorkItemStatus.InProgress,
            ["F18"] = WorkItemStatus.Approved,
            ["F19"] = WorkItemStatus.Submitted,
            ["F20"] = WorkItemStatus.Approved,
        };
        Assert.Equal(expected.OrderBy(e => e.Key), statuses.OrderBy(s => s.Key));
    }

    [Fact]
    public async Task SeedAsync_MidMonth_CaseySubmittedOwnItem()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();

        var item = await db.WorkItems.SingleAsync(w => w.Cycle!.Status == CycleStatus.Open && w.Facility!.Code == "F19");

        Assert.Equal(SeedData.Casey, item.AssigneeId);
        Assert.Equal(SeedData.Casey, item.SubmittedById);
    }

    [Fact]
    public async Task SeedAsync_MidMonth_AbsencesAreRelativeToToday()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();

        var absences = await db.Absences.OrderBy(a => a.UserId).ToListAsync();

        Assert.Equal(
            [
                (SeedData.Taylor, new DateOnly(2026, 11, 18), new DateOnly(2026, 11, 22), (int?)SeedData.Sam),
                (SeedData.Blake, new DateOnly(2026, 11, 17), new DateOnly(2026, 11, 23), (int?)SeedData.Alex),
                (SeedData.Dana, new DateOnly(2026, 11, 19), new DateOnly(2026, 11, 21), (int?)null),
            ],
            absences.Select(a => (a.UserId, a.StartDate, a.EndDate, a.CoverUserId)));
        Assert.All(absences, a => Assert.True(a.CreatedAt <= database.Calendar.UtcNow));
    }

    [Theory]
    [MemberData(nameof(SeedTimes))]
    public async Task SeedAsync_AnyDay_EventTrailsEndAtItemStatus(DateTimeOffset now)
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(now);
        await using var db = database.CreateContext();

        var items = await db.WorkItems.Include(w => w.Cycle).Include(w => w.Events).ToListAsync();

        Assert.Equal(40, items.Count);
        Assert.All(items, item =>
        {
            var events = item.Events.OrderBy(e => e.CreatedAt).ThenBy(e => e.Id).ToList();
            var created = events[0];
            Assert.Equal(WorkItemEventType.Created, created.Type);
            Assert.Equal(WorkItemStatus.NotStarted, created.ToStatus);
            Assert.Equal(item.Cycle!.OpenedAt, created.CreatedAt);

            var changes = events.Skip(1).ToList();
            Assert.All(changes, e => Assert.Equal(WorkItemEventType.StatusChanged, e.Type));
            var chain = events.Select(e => e.ToStatus).ToList();
            Assert.Equal(chain.SkipLast(1), changes.Select(e => e.FromStatus));
            Assert.Equal(item.Status, chain[^1]);
            Assert.Equal(1 + changes.Count, item.Version);

            Assert.Equal(events.Count, events.Select(e => e.CreatedAt).Distinct().Count());
            Assert.All(events, e => Assert.True(e.CreatedAt <= now.UtcDateTime, $"Event at {e.CreatedAt:O} is after now."));
            if (item.Cycle.ClosedAt is { } closedAt)
            {
                Assert.All(events, e => Assert.True(e.CreatedAt <= closedAt));
            }
        });
    }

    [Theory]
    [MemberData(nameof(SeedTimes))]
    public async Task SeedAsync_AnyDay_ItemFieldsMatchStatus(DateTimeOffset now)
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(now);
        await using var db = database.CreateContext();

        var items = await db.WorkItems.ToListAsync();

        Assert.All(items, item =>
        {
            var wasSubmitted = item.Status is WorkItemStatus.Submitted or WorkItemStatus.Returned or WorkItemStatus.Approved;
            Assert.Equal(wasSubmitted, item.SubmittedById is not null);
            Assert.Equal(wasSubmitted, item.Summary is not null);
            Assert.Equal(item.Status == WorkItemStatus.Approved, item.ApprovedById is not null);
            Assert.Equal(item.Status == WorkItemStatus.Blocked, item.BlockedReason is not null);
            if (item.ApprovedById is not null)
            {
                Assert.NotEqual(item.SubmittedById, item.ApprovedById);
            }
        });
    }

    [Theory]
    [MemberData(nameof(SeedTimes))]
    public async Task SeedAsync_AnyDay_ItemsHaveBalancesInRange(DateTimeOffset now)
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(now);
        await using var db = database.CreateContext();

        var items = await db.WorkItems.ToListAsync();

        Assert.All(items, item =>
        {
            var total = Assert.NotNull(item.ArTotalCents);
            var over90 = Assert.NotNull(item.ArOver90Cents);
            Assert.InRange(total, 150_000_00L, 1_200_000_00L);
            Assert.InRange(over90 * 100, total * 10, total * 45);
        });
    }

    [Fact]
    public async Task SeedAsync_TwoDifferentDays_SameBalancesPerFacility()
    {
        await using var first = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var second = await SqliteTestDatabase.CreateSeededAsync(January);
        await using var firstDb = first.CreateContext();
        await using var secondDb = second.CreateContext();

        var firstBalances = await CurrentBalancesAsync(firstDb);
        var secondBalances = await CurrentBalancesAsync(secondDb);

        Assert.Equal(firstBalances, secondBalances);
    }

    [Theory]
    [MemberData(nameof(SeedTimes))]
    public async Task SeedAsync_AnyDay_CurrentItemsLinkToLastMonth(DateTimeOffset now)
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(now);
        await using var db = database.CreateContext();

        var current = await db.WorkItems
            .Include(w => w.PreviousItem).ThenInclude(p => p!.Cycle)
            .Where(w => w.Cycle!.Status == CycleStatus.Open)
            .ToListAsync();

        Assert.Equal(20, current.Count);
        Assert.All(current, item =>
        {
            Assert.NotNull(item.PreviousItem);
            Assert.Equal(item.FacilityId, item.PreviousItem.FacilityId);
            Assert.Equal(CycleStatus.Closed, item.PreviousItem.Cycle!.Status);
        });
    }

    [Theory]
    [MemberData(nameof(SeedTimes))]
    public async Task SeedAsync_AnyDay_NoActorIsAway(DateTimeOffset now)
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(now);
        await using var db = database.CreateContext();

        var absences = await db.Absences.ToListAsync();
        var events = await db.WorkItemEvents.ToListAsync();

        Assert.All(events, e =>
        {
            var date = database.Calendar.DateOf(e.CreatedAt);
            Assert.DoesNotContain(absences, a => a.UserId == e.ActorId && a.StartDate <= date && date <= a.EndDate);
            if (e.OnBehalfOfId is { } ownerId)
            {
                Assert.Contains(absences, a =>
                    a.UserId == ownerId && a.CoverUserId == e.ActorId && a.StartDate <= date && date <= a.EndDate);
            }
        });
    }

    [Theory]
    [MemberData(nameof(FirstOfMonthTimes))]
    public async Task SeedAsync_FirstOfMonth_DanaActsBeforeHerAbsenceStarts(DateTimeOffset now)
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(now);
        await using var db = database.CreateContext();

        var absence = await db.Absences.SingleAsync(a => a.UserId == SeedData.Dana);
        var danaEvents = await db.WorkItemEvents
            .Where(e => e.ActorId == SeedData.Dana && e.WorkItem!.Cycle!.Status == CycleStatus.Open)
            .ToListAsync();

        Assert.Equal((new DateOnly(2026, 12, 2), new DateOnly(2026, 12, 4)), (absence.StartDate, absence.EndDate));
        Assert.Null(absence.CoverUserId);
        Assert.NotEmpty(danaEvents);
        Assert.All(danaEvents, e => Assert.True(database.Calendar.DateOf(e.CreatedAt) < absence.StartDate));
    }

    [Fact]
    public async Task SeedAsync_SecondOfMonth_CoversActForAbsentOwnersAndReviewers()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SecondOfMonth);
        await using var db = database.CreateContext();

        var coveredActions = await db.WorkItemEvents
            .Where(e => e.OnBehalfOfId != null)
            .Select(e => new { e.ActorId, e.OnBehalfOfId })
            .Distinct()
            .ToListAsync();

        Assert.Contains(coveredActions, e => e.ActorId == SeedData.Alex && e.OnBehalfOfId == SeedData.Blake);
        Assert.Contains(coveredActions, e => e.ActorId == SeedData.Sam && e.OnBehalfOfId == SeedData.Taylor);
    }

    [Fact]
    public async Task SeedAsync_NonEmptyDatabase_AddsNothing()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using (var db = database.CreateContext())
        {
            await SeedData.SeedAsync(db, database.Calendar);
        }

        await using var check = database.CreateContext();
        Assert.Equal(11, await check.Users.CountAsync());
        Assert.Equal(2, await check.ReviewCycles.CountAsync());
        Assert.Equal(40, await check.WorkItems.CountAsync());
        Assert.Equal(3, await check.Absences.CountAsync());
    }

    private static async Task<List<(int FacilityId, long? Total, long? Over90)>> CurrentBalancesAsync(AppDbContext db)
    {
        var rows = await db.WorkItems
            .Where(w => w.Cycle!.Status == CycleStatus.Open)
            .OrderBy(w => w.FacilityId)
            .Select(w => new { w.FacilityId, w.ArTotalCents, w.ArOver90Cents })
            .ToListAsync();
        return rows.Select(r => (r.FacilityId, r.ArTotalCents, r.ArOver90Cents)).ToList();
    }
}
