using Rcm.Api.Domain;
using static Rcm.Api.Domain.CycleStatus;
using static Rcm.Api.Domain.WorkItemStatus;

namespace Rcm.Api.Tests.Domain;

public class CycleRulesTests
{
    private static readonly DateOnly Today = new(2026, 11, 19);
    private static readonly CycleSnapshot October = new(2026, 10, Closed);
    private static readonly CycleSnapshot November = new(2026, 11, Open);
    private static readonly CycleSnapshot NovemberClosed = November with { Status = Closed };

    [Fact]
    public void NextToOpen_LatestNovember_ReturnsDecember()
    {
        Assert.Equal(new YearMonth(2026, 12), CycleRules.NextToOpen([October, NovemberClosed], Today));
    }

    [Fact]
    public void NextToOpen_LatestDecember_RollsOverToJanuary()
    {
        Assert.Equal(new YearMonth(2027, 1), CycleRules.NextToOpen([new(2026, 12, Closed), October], Today));
    }

    [Fact]
    public void NextToOpen_NoMonths_ReturnsCurrentMonth()
    {
        Assert.Equal(new YearMonth(2026, 11), CycleRules.NextToOpen([], Today));
    }

    [Fact]
    public void DefaultDueDate_AnyMonth_IsThe15th()
    {
        Assert.Equal(new DateOnly(2026, 12, 15), CycleRules.DefaultDueDate(new YearMonth(2026, 12)));
    }

    [Fact]
    public void CanOpen_ManagerNoOpenMonthDueInside_ReturnsNextMonth()
    {
        var result = CycleRules.CanOpen(Role.Manager, [October, NovemberClosed], Today, new DateOnly(2026, 12, 15));

        Assert.Equal(new YearMonth(2026, 12), result.Value);
    }

    [Theory]
    [InlineData(Role.Supervisor)]
    [InlineData(Role.Biller)]
    public void CanOpen_NotManager_ReturnsForbidden(Role role)
    {
        var result = CycleRules.CanOpen(role, [NovemberClosed], Today, new DateOnly(2026, 12, 15));

        Assert.Equal(ErrorKind.Forbidden, result.Error?.Kind);
    }

    [Fact]
    public void CanOpen_MonthAlreadyOpen_ReturnsInvalid()
    {
        var result = CycleRules.CanOpen(Role.Manager, [October, November], Today, new DateOnly(2026, 12, 15));

        Assert.Equal(ErrorKind.Invalid, result.Error?.Kind);
    }

    [Theory]
    [InlineData(2026, 11, 30)]
    [InlineData(2027, 1, 1)]
    public void CanOpen_DueDateOutsideMonth_ReturnsInvalid(int year, int month, int day)
    {
        var result = CycleRules.CanOpen(Role.Manager, [NovemberClosed], Today, new DateOnly(year, month, day));

        Assert.Equal(ErrorKind.Invalid, result.Error?.Kind);
        Assert.Equal("The due date must fall inside December 2026.", result.Error?.Message);
    }

    [Fact]
    public void CanOpen_NoDueDate_ReturnsInvalid()
    {
        var result = CycleRules.CanOpen(Role.Manager, [NovemberClosed], Today, null);

        Assert.Equal(ErrorKind.Invalid, result.Error?.Kind);
    }

    [Fact]
    public void CanClose_ManagerOpenMonth_Succeeds()
    {
        Assert.True(CycleRules.CanClose(Role.Manager, November).IsSuccess);
    }

    [Fact]
    public void CanClose_SupervisorOpenMonth_ReturnsForbidden()
    {
        Assert.Equal(ErrorKind.Forbidden, CycleRules.CanClose(Role.Supervisor, November).Error?.Kind);
    }

    [Fact]
    public void CanClose_ClosedMonth_ReturnsInvalid()
    {
        Assert.Equal(ErrorKind.Invalid, CycleRules.CanClose(Role.Manager, October).Error?.Kind);
    }

    [Fact]
    public void ActionsFor_ManagerShowingOpenMonth_CanCloseOnly()
    {
        var actions = CycleRules.ActionsFor(Role.Manager, November, [October, November], Today);

        Assert.Equal(new MonthActionsDecision(true, null), actions);
    }

    [Fact]
    public void ActionsFor_ManagerNoOpenMonth_OffersNextMonth()
    {
        var actions = CycleRules.ActionsFor(Role.Manager, October, [October, NovemberClosed], Today);

        Assert.Equal(new MonthActionsDecision(false, new YearMonth(2026, 12)), actions);
    }

    [Fact]
    public void ActionsFor_ManagerShowingClosedMonthWhileAnotherIsOpen_Nothing()
    {
        var actions = CycleRules.ActionsFor(Role.Manager, October, [October, November], Today);

        Assert.Equal(new MonthActionsDecision(false, null), actions);
    }

    [Fact]
    public void ActionsFor_Supervisor_Nothing()
    {
        var actions = CycleRules.ActionsFor(Role.Supervisor, NovemberClosed, [NovemberClosed], Today);

        Assert.Equal(new MonthActionsDecision(false, null), actions);
    }

    [Theory]
    [InlineData(NotStarted, true)]
    [InlineData(InProgress, true)]
    [InlineData(Blocked, true)]
    [InlineData(Submitted, true)]
    [InlineData(Returned, true)]
    [InlineData(Approved, false)]
    [InlineData(Incomplete, false)]
    public void ClosesAsIncomplete_EachStatus_MatchesDocs(WorkItemStatus status, bool expected)
    {
        Assert.Equal(expected, CycleRules.ClosesAsIncomplete(status));
    }

    [Fact]
    public void NewItem_FacilityOwner_NotStartedWithCreatedEvent()
    {
        var cycle = new ReviewCycle { Year = 2026, Month = 12, DueDate = new DateOnly(2026, 12, 15) };
        var now = new DateTime(2026, 12, 1, 13, 0, 0, DateTimeKind.Utc);

        var item = CycleRules.NewItem(cycle, facilityId: 4, ownerId: 7, previousItemId: 24, actorId: 1, now);

        Assert.Equal((4, 7, NotStarted, 24), (item.FacilityId, item.AssigneeId, item.Status, item.PreviousItemId));
        Assert.Equal(cycle.DueDate, item.DueDate);
        Assert.Null(item.ArTotalCents);
        var created = Assert.Single(item.Events);
        Assert.Equal((WorkItemEventType.Created, NotStarted, 7, 1, now), (created.Type, created.ToStatus, created.ToUserId, created.ActorId, created.CreatedAt));
    }

    [Fact]
    public void CloseItem_Submitted_IncompleteWithEventAndNewVersion()
    {
        var item = new WorkItem { Status = Submitted, Version = 3 };
        var now = new DateTime(2026, 12, 1, 13, 0, 0, DateTimeKind.Utc);

        var change = CycleRules.CloseItem(item, actorId: 1, now);

        Assert.Equal((Incomplete, 4), (item.Status, item.Version));
        Assert.Equal((WorkItemEventType.StatusChanged, Submitted, Incomplete, 1), (change.Type, change.FromStatus, change.ToStatus, change.ActorId));
        Assert.Same(change, Assert.Single(item.Events));
    }
}
