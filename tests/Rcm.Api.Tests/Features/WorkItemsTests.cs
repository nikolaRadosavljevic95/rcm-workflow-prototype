using Microsoft.EntityFrameworkCore;
using Rcm.Api.Auth;
using Rcm.Api.Data;
using Rcm.Api.Domain;
using Rcm.Api.Features;
using Rcm.Api.Features.WorkItems;
using Rcm.Api.Tests.TestSupport;

namespace Rcm.Api.Tests.Features;

public class WorkItemsTests
{
    private static readonly CurrentUser Morgan = new(SeedData.Morgan, "Morgan", Role.Manager);
    private static readonly CurrentUser Quinn = new(SeedData.Quinn, "Quinn", Role.Manager);
    private static readonly CurrentUser Sam = new(SeedData.Sam, "Sam", Role.Supervisor);
    private static readonly CurrentUser Jordan = new(SeedData.Jordan, "Jordan", Role.Supervisor);
    private static readonly CurrentUser Casey = new(SeedData.Casey, "Casey", Role.Supervisor);
    private static readonly CurrentUser Alex = new(SeedData.Alex, "Alex", Role.Biller);
    private static readonly CurrentUser Blake = new(SeedData.Blake, "Blake", Role.Biller);

    [Fact]
    public async Task GetMyWork_Biller_GroupsOwnAndCoveredItemsByArOver90()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();

        var result = await new GetMyWorkHandler(Reader(db), Contexts(db, database)).HandleAsync(Alex, null, CancellationToken.None);

        Assert.Equal(CycleStatus.Open, result.Value.Cycle.Status);
        Assert.Equal(["F03", "F07", "F02", "F01"], result.Value.NeedsAction.Select(r => r.Facility.Code));
        Assert.Equal(["F08", "F05", "F04", "F06"], result.Value.WaitingOrDone.Select(r => r.Facility.Code));
        Assert.All(result.Value.NeedsAction, r => Assert.Contains(ItemFlag.Overdue, r.Flags));
    }

    [Fact]
    public async Task GetMyWork_CoveredOwnersItem_TaggedCoveringForOwner()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();

        var result = await new GetMyWorkHandler(Reader(db), Contexts(db, database)).HandleAsync(Alex, null, CancellationToken.None);

        var blakes = result.Value.NeedsAction.Single(r => r.Facility.Code == "F07");
        Assert.Equal("Blake", blakes.CoveringFor?.Name);
        Assert.Equal("Alex", blakes.CoveredBy?.Name);
        Assert.Contains(ItemFlag.Covered, blakes.Flags);
        Assert.Equal([WorkflowAction.Start, WorkflowAction.Block, WorkflowAction.Submit], blakes.AllowedActions);
        var approved = result.Value.WaitingOrDone.Single(r => r.Facility.Code == "F05");
        Assert.Null(approved.CoveringFor);
        Assert.DoesNotContain(ItemFlag.Covered, approved.Flags);
        Assert.Null(result.Value.NeedsAction.Single(r => r.Facility.Code == "F01").CoveringFor);
    }

    [Fact]
    public async Task GetMyWork_OwnerRow_CarriesReviewerTeamAndActions()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();

        var result = await new GetMyWorkHandler(Reader(db), Contexts(db, database)).HandleAsync(Alex, null, CancellationToken.None);

        var blocked = result.Value.NeedsAction.Single(r => r.Facility.Code == "F02");
        Assert.Equal(("Alex", "Sam", "Sam"), (blocked.Owner.Name, blocked.Reviewer?.Name, blocked.Team.Name));
        Assert.Equal([WorkflowAction.Unblock], blocked.AllowedActions);
        Assert.Equal(41_230_000, blocked.ArTotalCents);
    }

    [Fact]
    public async Task GetOverview_Biller_ReturnsForbidden()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();

        var result = await new GetOverviewHandler(Reader(db), Contexts(db, database)).HandleAsync(Alex, null, CancellationToken.None);

        Assert.Equal(ErrorKind.Forbidden, result.Error?.Kind);
    }

    [Fact]
    public async Task GetOverview_Supervisor_SeesTeamWithoutManagerSections()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();

        var overview = (await new GetOverviewHandler(Reader(db), Contexts(db, database)).HandleAsync(Jordan, null, CancellationToken.None)).Value;

        Assert.Equal(["F13", "F14", "F15"], overview.Items.Select(r => r.Facility.Code).Order());
        Assert.Equal(3, overview.NeedsAttention.Count);
        Assert.Empty(overview.WaitingForMyReview);
        Assert.Equal(
            [("Dana", 3, 0, 0, (DateOnly?)new DateOnly(2026, 11, 21), (string?)null)],
            overview.People.Select(p => (p.Person.Name, p.Open, p.Submitted, p.Approved, p.AwayUntil, p.Cover?.Name)));
        Assert.All(overview.Items, r => Assert.Contains(ItemFlag.NeedsCover, r.Flags));
        Assert.Null(overview.Progress);
        Assert.Null(overview.Teams);
    }

    [Fact]
    public async Task GetOverview_ReviewersCover_SeesSubmittedItemWaiting()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();

        var overview = (await new GetOverviewHandler(Reader(db), Contexts(db, database)).HandleAsync(Sam, null, CancellationToken.None)).Value;

        Assert.Equal(["F09", "F04"], overview.WaitingForMyReview.Select(r => r.Facility.Code));
        Assert.Contains(overview.Items, r => r.Owner.Name == "Charlie");
        Assert.Contains(overview.People, p => p.Person.Name == "Blake" && p.Cover?.Name == "Alex");
    }

    [Fact]
    public async Task Transition_CoverActsForOwner_RecordsOnBehalfOf()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();
        var (blakesItem, version) = await OpenItemAsync(db, "F07");

        var detail = (await Transition(db, database, Alex, blakesItem, WorkflowAction.Start, null, version)).Value;

        Assert.Equal(WorkItemStatus.InProgress, detail.Item.Status);
        Assert.Equal(("Alex", "Blake"), (detail.Events[0].Actor.Name, detail.Events[0].OnBehalfOf?.Name));
    }

    [Fact]
    public async Task Transition_ReviewersCoverApproves_RecordsOnBehalfOfReviewer()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();
        var (charliesItem, version) = await OpenItemAsync(db, "F09");

        var detail = (await Transition(db, database, Sam, charliesItem, WorkflowAction.Approve, null, version)).Value;

        Assert.Equal(WorkItemStatus.Approved, detail.Item.Status);
        Assert.Equal(("Sam", "Taylor"), (detail.Events[0].Actor.Name, detail.Events[0].OnBehalfOf?.Name));
    }

    [Fact]
    public async Task GetOverview_Manager_CountsProgressAndTeams()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();

        var overview = (await new GetOverviewHandler(Reader(db), Contexts(db, database)).HandleAsync(Morgan, null, CancellationToken.None)).Value;

        Assert.Equal(20, overview.Items.Count);
        Assert.Equal(
            [
                (WorkItemStatus.Approved, 9), (WorkItemStatus.Submitted, 3), (WorkItemStatus.Returned, 1),
                (WorkItemStatus.InProgress, 3), (WorkItemStatus.Blocked, 2), (WorkItemStatus.NotStarted, 2),
            ],
            overview.Progress!.Select(p => (p.Status, p.Count)));
        Assert.Equal(
            [("Casey", 5, 3, 1, 1, 2), ("Jordan", 3, 0, 0, 3, 3), ("Sam", 8, 3, 1, 4, 5), ("Taylor", 4, 3, 1, 0, 1)],
            overview.Teams!.Select(t => (t.Supervisor.Name, t.Items, t.Approved, t.Submitted, t.Open, t.Overdue)));
    }

    [Fact]
    public async Task GetOverview_ReviewerOfSupervisor_SeesSubmittedItemWaiting()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();
        var handler = new GetOverviewHandler(Reader(db), Contexts(db, database));

        var quinn = (await handler.HandleAsync(Quinn, null, CancellationToken.None)).Value;
        var morgan = (await handler.HandleAsync(Morgan, null, CancellationToken.None)).Value;
        var casey = (await handler.HandleAsync(Casey, null, CancellationToken.None)).Value;

        Assert.Equal(["F19"], quinn.WaitingForMyReview.Select(r => r.Facility.Code));
        Assert.Empty(morgan.WaitingForMyReview);
        Assert.Equal([WorkflowAction.Approve, WorkflowAction.Return], morgan.Items.Single(r => r.Facility.Code == "F19").AllowedActions);
        Assert.Empty(casey.Items.Single(r => r.Facility.Code == "F19").AllowedActions);
    }

    [Fact]
    public async Task GetOverview_ClosedMonth_HasNoActions()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();
        var closedId = await db.ReviewCycles.Where(c => c.Status == CycleStatus.Closed).Select(c => c.Id).SingleAsync();

        var overview = (await new GetOverviewHandler(Reader(db), Contexts(db, database)).HandleAsync(Morgan, closedId, CancellationToken.None)).Value;

        Assert.Equal(CycleStatus.Closed, overview.Cycle.Status);
        Assert.All(overview.Items, r => Assert.Empty(r.AllowedActions));
    }

    [Fact]
    public async Task GetOverview_UnknownMonth_ReturnsNotFound()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();

        var result = await new GetOverviewHandler(Reader(db), Contexts(db, database)).HandleAsync(Morgan, 999, CancellationToken.None);

        Assert.Equal(ErrorKind.NotFound, result.Error?.Kind);
    }

    [Fact]
    public async Task GetWorkItem_NotVisible_ReturnsNotFound()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();
        var charliesItem = await OpenItemIdAsync(db, "F09");

        var result = await new GetWorkItemHandler(Reader(db), Contexts(db, database)).HandleAsync(Alex, charliesItem, CancellationToken.None);

        Assert.Equal(ErrorKind.NotFound, result.Error?.Kind);
    }

    [Fact]
    public async Task GetWorkItem_Visible_ReturnsHistoryNewestFirstAndLastMonth()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();
        var itemId = await OpenItemIdAsync(db, "F02");

        var detail = (await new GetWorkItemHandler(Reader(db), Contexts(db, database)).HandleAsync(Alex, itemId, CancellationToken.None)).Value;

        Assert.Equal(
            [WorkItemStatus.Blocked, WorkItemStatus.InProgress, WorkItemStatus.NotStarted],
            detail.Events.Select(e => e.ToStatus));
        Assert.Equal("Morgan", detail.Events[^1].Actor.Name);
        Assert.NotNull(detail.BlockedReason);
        Assert.Equal(WorkItemStatus.Approved, detail.LastMonth?.Status);
        Assert.NotNull(detail.LastMonth?.Summary);
    }

    [Fact]
    public async Task Transition_BlockByOwner_WritesFieldsAndEvent()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();
        var (itemId, version) = await OpenItemAsync(db, "F01");

        var detail = (await Transition(db, database, Alex, itemId, WorkflowAction.Block, "Waiting for the pending list", version)).Value;

        Assert.Equal(WorkItemStatus.Blocked, detail.Item.Status);
        Assert.Equal(version + 1, detail.Item.Version);
        Assert.Equal("Waiting for the pending list", detail.BlockedReason);
        var latest = detail.Events[0];
        Assert.Equal((WorkItemEventType.StatusChanged, "Alex"), (latest.Type, latest.Actor.Name));
        Assert.Equal((WorkItemStatus.InProgress, WorkItemStatus.Blocked), (latest.FromStatus, latest.ToStatus));
    }

    [Fact]
    public async Task Transition_NotVisible_ReturnsNotFound()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();
        var (danasItem, version) = await OpenItemAsync(db, "F14");

        var result = await Transition(db, database, Alex, danasItem, WorkflowAction.Start, null, version);

        Assert.Equal(ErrorKind.NotFound, result.Error?.Kind);
    }

    [Fact]
    public async Task Transition_StaleVersion_ReturnsConflict()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();
        var (itemId, version) = await OpenItemAsync(db, "F01");

        var result = await Transition(db, database, Alex, itemId, WorkflowAction.Submit, "Done", version - 1);

        Assert.Equal(ErrorKind.Conflict, result.Error?.Kind);
    }

    [Fact]
    public async Task Transition_SameVersionTwice_SecondReturnsConflict()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        var (itemId, version) = await WithContext(database, db => OpenItemAsync(db, "F01"));

        var first = await WithContext(database, db => Transition(db, database, Alex, itemId, WorkflowAction.Submit, "Done", version));
        var second = await WithContext(database, db => Transition(db, database, Alex, itemId, WorkflowAction.Block, "Wait", version));

        Assert.True(first.IsSuccess);
        Assert.Equal(ErrorKind.Conflict, second.Error?.Kind);
    }

    [Fact]
    public async Task Transition_OwnerAndCoverActTogether_SecondReturnsConflict()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        var (blakesItem, version) = await WithContext(database, db => OpenItemAsync(db, "F07"));
        Result<WorkItemDetail>? owner = null;

        // Blake saves between Alex's read and Alex's write.
        await using var db = database.CreateContextRacing(async other =>
            owner = await Transition(other, database, Blake, blakesItem, WorkflowAction.Block, "Waiting on a statement", version));
        var cover = await Transition(db, database, Alex, blakesItem, WorkflowAction.Start, null, version);

        Assert.True(owner?.IsSuccess);
        Assert.Equal(ErrorKind.Conflict, cover.Error?.Kind);
        await using var check = database.CreateContext();
        var item = await check.WorkItems.SingleAsync(w => w.Id == blakesItem);
        Assert.Equal((WorkItemStatus.Blocked, version + 1), (item.Status, item.Version));
    }

    [Fact]
    public async Task SaveChanges_ItemChangedSinceRead_ThrowsConcurrencyException()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var first = database.CreateContext();
        await using var second = database.CreateContext();
        var itemId = await OpenItemIdAsync(first, "F01");
        var firstCopy = await first.WorkItems.SingleAsync(w => w.Id == itemId);
        var secondCopy = await second.WorkItems.SingleAsync(w => w.Id == itemId);

        firstCopy.Version++;
        await first.SaveChangesAsync();
        secondCopy.Version++;

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public async Task Transition_SupervisorNotCovering_ReturnsForbidden()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();
        var (blakesItem, version) = await OpenItemAsync(db, "F07");

        var result = await Transition(db, database, Sam, blakesItem, WorkflowAction.Start, null, version);

        Assert.Equal(ErrorKind.Forbidden, result.Error?.Kind);
    }

    [Fact]
    public async Task Transition_SubmitterApproves_ReturnsForbiddenReviewerSucceeds()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();
        var (itemId, version) = await OpenItemAsync(db, "F19");

        var byCasey = await Transition(db, database, Casey, itemId, WorkflowAction.Approve, null, version);
        var byQuinn = await Transition(db, database, Quinn, itemId, WorkflowAction.Approve, null, version);

        Assert.Equal(ErrorKind.Forbidden, byCasey.Error?.Kind);
        Assert.Equal(WorkItemStatus.Approved, byQuinn.Value.Item.Status);
        var stored = await db.WorkItems.AsNoTracking().SingleAsync(w => w.Id == itemId);
        Assert.Equal(SeedData.Quinn, stored.ApprovedById);
    }

    [Fact]
    public async Task Transition_ClosedMonth_ReturnsInvalid()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();
        var item = await db.WorkItems.SingleAsync(w => w.Cycle!.Status == CycleStatus.Closed && w.Facility!.Code == "F01");

        var result = await Transition(db, database, Alex, item.Id, WorkflowAction.Start, null, item.Version);

        Assert.Equal(ErrorKind.Invalid, result.Error?.Kind);
    }

    [Fact]
    public async Task Transition_MissingText_ReturnsInvalidAndSavesNothing()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        var (itemId, version) = await WithContext(database, db => OpenItemAsync(db, "F01"));

        var result = await WithContext(database, db => Transition(db, database, Alex, itemId, WorkflowAction.Block, " ", version));

        Assert.Equal(ErrorKind.Invalid, result.Error?.Kind);
        var after = await WithContext(database, db => OpenItemAsync(db, "F01"));
        Assert.Equal(version, after.Version);
    }

    [Fact]
    public async Task AddComment_VisibleItem_AddsEventAndKeepsVersion()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();
        var (itemId, version) = await OpenItemAsync(db, "F02");

        var detail = (await Comment(db, database, Sam, itemId, " Called the facility. ")).Value;

        Assert.Equal(version, detail.Item.Version);
        var latest = detail.Events[0];
        Assert.Equal((WorkItemEventType.Commented, "Sam", "Called the facility."), (latest.Type, latest.Actor.Name, latest.Text));
        Assert.Null(latest.ToStatus);
    }

    [Fact]
    public async Task AddComment_CoverOfOwner_RecordsOnBehalfOf()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();
        var blakesItem = await OpenItemIdAsync(db, "F07");

        var detail = (await Comment(db, database, Alex, blakesItem, "Asked for the aging report.")).Value;

        Assert.Equal(("Alex", "Blake"), (detail.Events[0].Actor.Name, detail.Events[0].OnBehalfOf?.Name));
    }

    [Fact]
    public async Task AddComment_NotVisible_ReturnsNotFound()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();
        var charliesItem = await OpenItemIdAsync(db, "F09");

        var result = await Comment(db, database, Alex, charliesItem, "Note");

        Assert.Equal(ErrorKind.NotFound, result.Error?.Kind);
    }

    [Fact]
    public async Task AddComment_ClosedMonthOrNoText_ReturnsInvalid()
    {
        await using var database = await SqliteTestDatabase.CreateSeededAsync(SqliteTestDatabase.MidMonth);
        await using var db = database.CreateContext();
        var closedItem = await db.WorkItems.Where(w => w.Cycle!.Status == CycleStatus.Closed && w.Facility!.Code == "F01")
            .Select(w => w.Id).SingleAsync();
        var openItem = await OpenItemIdAsync(db, "F01");

        Assert.Equal(ErrorKind.Invalid, (await Comment(db, database, Alex, closedItem, "Late note")).Error?.Kind);
        Assert.Equal(ErrorKind.Invalid, (await Comment(db, database, Alex, openItem, "")).Error?.Kind);
    }

    private static WorkItemReader Reader(AppDbContext db) => new(db);

    private static RequestContextLoader Contexts(AppDbContext db, SqliteTestDatabase database) => new(db, database.Calendar);

    private static Task<Result<WorkItemDetail>> Transition(
        AppDbContext db, SqliteTestDatabase database, CurrentUser user, int itemId, WorkflowAction action, string? text, int version) =>
        new TransitionWorkItemHandler(db, Reader(db), Contexts(db, database), database.Calendar)
            .HandleAsync(user, itemId, new TransitionRequest(action, text, version), CancellationToken.None);

    private static Task<Result<WorkItemDetail>> Comment(
        AppDbContext db, SqliteTestDatabase database, CurrentUser user, int itemId, string? text) =>
        new AddCommentHandler(db, Reader(db), Contexts(db, database), database.Calendar)
            .HandleAsync(user, itemId, new CommentRequest(text), CancellationToken.None);

    private static async Task<T> WithContext<T>(SqliteTestDatabase database, Func<AppDbContext, Task<T>> work)
    {
        await using var db = database.CreateContext();
        return await work(db);
    }

    private static async Task<int> OpenItemIdAsync(AppDbContext db, string code) => (await OpenItemAsync(db, code)).Id;

    private static async Task<(int Id, int Version)> OpenItemAsync(AppDbContext db, string code)
    {
        var item = await db.WorkItems
            .AsNoTracking()
            .Where(w => w.Cycle!.Status == CycleStatus.Open && w.Facility!.Code == code)
            .Select(w => new { w.Id, w.Version })
            .SingleAsync();
        return (item.Id, item.Version);
    }
}
