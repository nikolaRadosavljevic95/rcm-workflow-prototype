using Rcm.Api.Domain;
using Rcm.Api.Tests.TestSupport;
using static Rcm.Api.Domain.WorkflowAction;
using static Rcm.Api.Domain.WorkItemStatus;
using static Rcm.Api.Tests.TestSupport.TestOrg;

namespace Rcm.Api.Tests.Domain;

public class WorkflowRulesTests
{
    private static readonly DateOnly DueDate = new(2026, 11, 18);
    private static readonly DateTime Now = new(2026, 11, 19, 15, 0, 0, DateTimeKind.Utc);

    // Every row of the docs/02 transition table: from, action, to.
    public static TheoryData<WorkItemStatus, WorkflowAction, WorkItemStatus> TableRows => new()
    {
        { NotStarted, Start, InProgress },
        { NotStarted, Block, Blocked },
        { InProgress, Block, Blocked },
        { Returned, Block, Blocked },
        { Blocked, Unblock, InProgress },
        { NotStarted, Submit, Submitted },
        { InProgress, Submit, Submitted },
        { Returned, Submit, Submitted },
        { Submitted, Approve, Approved },
        { Submitted, Return, Returned },
    };

    public static TheoryData<WorkItemStatus, WorkflowAction> PairsInTable
    {
        get
        {
            var data = new TheoryData<WorkItemStatus, WorkflowAction>();
            foreach (var row in TableRows)
            {
                data.Add((WorkItemStatus)row[0], (WorkflowAction)row[1]);
            }

            return data;
        }
    }

    public static TheoryData<WorkItemStatus, WorkflowAction> PairsOutsideTable
    {
        get
        {
            var inTable = TableRows.Select(row => ((WorkItemStatus)row[0], (WorkflowAction)row[1])).ToHashSet();
            var data = new TheoryData<WorkItemStatus, WorkflowAction>();
            foreach (var status in Enum.GetValues<WorkItemStatus>())
            {
                foreach (var action in Enum.GetValues<WorkflowAction>().Where(a => !inTable.Contains((status, a))))
                {
                    data.Add(status, action);
                }
            }

            return data;
        }
    }

    public static TheoryData<WorkflowAction, WorkItemStatus> OwnerActions => new()
    {
        { Start, NotStarted },
        { Block, InProgress },
        { Unblock, Blocked },
        { Submit, InProgress },
    };

    public static TheoryData<WorkflowAction> ReviewActions => [Approve, Return];

    public static TheoryData<WorkflowAction> TextActions => [Block, Submit, Return];

    [Theory]
    [MemberData(nameof(TableRows))]
    public void Decide_RowInTable_MovesToTargetStatus(WorkItemStatus from, WorkflowAction action, WorkItemStatus to)
    {
        var result = WorkflowRules.Decide(AllowedActorFor(action), Item(from), Chart, action, "Notes");

        Assert.True(result.IsSuccess);
        Assert.Equal(to, result.Value.To);
    }

    [Theory]
    [MemberData(nameof(PairsOutsideTable))]
    public void Decide_PairOutsideTable_ReturnsInvalid(WorkItemStatus from, WorkflowAction action)
    {
        var result = WorkflowRules.Decide(AllowedActorFor(action), Item(from), Chart, action, "Notes");

        Assert.Equal(ErrorKind.Invalid, result.Error?.Kind);
    }

    [Theory]
    [MemberData(nameof(PairsInTable))]
    public void Decide_ClosedMonth_ReturnsInvalid(WorkItemStatus from, WorkflowAction action)
    {
        var result = WorkflowRules.Decide(AllowedActorFor(action), Item(from, monthOpen: false), Chart, action, "Notes");

        Assert.Equal(ErrorKind.Invalid, result.Error?.Kind);
    }

    [Theory]
    [MemberData(nameof(OwnerActions))]
    public void Decide_OwnerActionByOwner_ActsForThemselves(WorkflowAction action, WorkItemStatus from)
    {
        var result = WorkflowRules.Decide(Actor(Alex), Item(from), Chart, action, "Notes");

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.OnBehalfOfId);
    }

    [Theory]
    [MemberData(nameof(OwnerActions))]
    public void Decide_OwnerActionByOwnersCover_ActsOnBehalfOfOwner(WorkflowAction action, WorkItemStatus from)
    {
        var result = WorkflowRules.Decide(Actor(Blake, Alex), Item(from), Chart, action, "Notes");

        Assert.True(result.IsSuccess);
        Assert.Equal(Alex, result.Value.OnBehalfOfId);
    }

    [Theory]
    [MemberData(nameof(OwnerActions))]
    public void Decide_OwnerActionBySupervisorNotCovering_ReturnsForbidden(WorkflowAction action, WorkItemStatus from)
    {
        var result = WorkflowRules.Decide(Actor(Sam), Item(from), Chart, action, "Notes");

        Assert.Equal(ErrorKind.Forbidden, result.Error?.Kind);
    }

    [Theory]
    [MemberData(nameof(OwnerActions))]
    public void Decide_OwnerActionByManagerOrPeer_ReturnsForbidden(WorkflowAction action, WorkItemStatus from)
    {
        Assert.Equal(ErrorKind.Forbidden, WorkflowRules.Decide(Actor(Morgan), Item(from), Chart, action, "Notes").Error?.Kind);
        Assert.Equal(ErrorKind.Forbidden, WorkflowRules.Decide(Actor(Blake), Item(from), Chart, action, "Notes").Error?.Kind);
    }

    [Theory]
    [MemberData(nameof(ReviewActions))]
    public void Decide_ReviewByReviewer_ActsForThemselves(WorkflowAction action)
    {
        var result = WorkflowRules.Decide(Actor(Sam), Item(Submitted), Chart, action, "Notes");

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.OnBehalfOfId);
    }

    [Theory]
    [MemberData(nameof(ReviewActions))]
    public void Decide_ReviewByReviewersCover_ActsOnBehalfOfReviewer(WorkflowAction action)
    {
        var result = WorkflowRules.Decide(Actor(Taylor, Sam), Item(Submitted), Chart, action, "Notes");

        Assert.True(result.IsSuccess);
        Assert.Equal(Sam, result.Value.OnBehalfOfId);
    }

    [Theory]
    [MemberData(nameof(ReviewActions))]
    public void Decide_ReviewByAnyManager_Succeeds(WorkflowAction action)
    {
        var result = WorkflowRules.Decide(Actor(Quinn), Item(Submitted), Chart, action, "Notes");

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.OnBehalfOfId);
    }

    [Theory]
    [MemberData(nameof(ReviewActions))]
    public void Decide_ReviewByOtherSupervisorOrBiller_ReturnsForbidden(WorkflowAction action)
    {
        Assert.Equal(ErrorKind.Forbidden, WorkflowRules.Decide(Actor(Jordan), Item(Submitted), Chart, action, "Notes").Error?.Kind);
        Assert.Equal(ErrorKind.Forbidden, WorkflowRules.Decide(Actor(Charlie), Item(Submitted), Chart, action, "Notes").Error?.Kind);
    }

    [Theory]
    [MemberData(nameof(ReviewActions))]
    public void Decide_SupervisorCoveredAndSubmitted_SupervisorForbiddenManagerAllowed(WorkflowAction action)
    {
        var item = Item(Submitted, submittedById: Sam);

        Assert.Equal(ErrorKind.Forbidden, WorkflowRules.Decide(Actor(Sam, Alex), item, Chart, action, "Notes").Error?.Kind);
        Assert.True(WorkflowRules.Decide(Actor(Morgan), item, Chart, action, "Notes").IsSuccess);
    }

    [Theory]
    [MemberData(nameof(ReviewActions))]
    public void Decide_ReviewByOwnerAfterCoverSubmitted_ReturnsForbidden(WorkflowAction action)
    {
        var result = WorkflowRules.Decide(Actor(Alex), Item(Submitted, submittedById: Blake), Chart, action, "Notes");

        Assert.Equal(ErrorKind.Forbidden, result.Error?.Kind);
    }

    [Fact]
    public void Decide_ItemOwnedBySupervisor_ManagerReviewsPeerCannot()
    {
        var item = Item(Submitted, ownerId: Casey);

        Assert.True(WorkflowRules.Decide(Actor(Quinn), item, Chart, Approve, null).IsSuccess);
        Assert.Equal(ErrorKind.Forbidden, WorkflowRules.Decide(Actor(Casey), item, Chart, Approve, null).Error?.Kind);
        Assert.Equal(ErrorKind.Forbidden, WorkflowRules.Decide(Actor(Jordan), item, Chart, Approve, null).Error?.Kind);
    }

    [Fact]
    public void Decide_OwnerReportsToNoOne_AnyOtherManagerReviews()
    {
        var item = Item(Submitted, ownerId: Morgan);

        Assert.True(WorkflowRules.Decide(Actor(Quinn), item, Chart, Approve, null).IsSuccess);
        Assert.Equal(ErrorKind.Forbidden, WorkflowRules.Decide(Actor(Morgan), item, Chart, Approve, null).Error?.Kind);
    }

    [Theory]
    [MemberData(nameof(TextActions))]
    public void Decide_TextActionWithoutText_ReturnsInvalid(WorkflowAction action)
    {
        var item = Item(action == Return ? Submitted : InProgress);
        var actor = AllowedActorFor(action);

        Assert.Equal(ErrorKind.Invalid, WorkflowRules.Decide(actor, item, Chart, action, null).Error?.Kind);
        Assert.Equal(ErrorKind.Invalid, WorkflowRules.Decide(actor, item, Chart, action, "").Error?.Kind);
        Assert.Equal(ErrorKind.Invalid, WorkflowRules.Decide(actor, item, Chart, action, "   ").Error?.Kind);
    }

    [Fact]
    public void Decide_TextOverLimit_ReturnsInvalid()
    {
        var reason = new string('x', WorkflowRules.MaxBlockedReasonLength + 1);
        var summary = new string('x', WorkflowRules.MaxTextLength + 1);

        Assert.Equal(ErrorKind.Invalid, WorkflowRules.Decide(Actor(Alex), Item(InProgress), Chart, Block, reason).Error?.Kind);
        Assert.Equal(ErrorKind.Invalid, WorkflowRules.Decide(Actor(Alex), Item(InProgress), Chart, Submit, summary).Error?.Kind);
    }

    [Fact]
    public void Decide_TextWithSpaces_KeepsTrimmedText()
    {
        var result = WorkflowRules.Decide(Actor(Alex), Item(InProgress), Chart, Block, "  Waiting for the pending list  ");

        Assert.Equal("Waiting for the pending list", result.Value.Text);
    }

    [Fact]
    public void Decide_ActionWithoutText_DropsText()
    {
        var result = WorkflowRules.Decide(Actor(Alex), Item(NotStarted), Chart, Start, "Ignored");

        Assert.Null(result.Value.Text);
    }

    [Fact]
    public void AllowedActions_OwnerByStatus_ListsOwnerActionsOnly()
    {
        Assert.Equal([Start, Block, Submit], WorkflowRules.AllowedActions(Actor(Alex), Item(NotStarted), Chart));
        Assert.Equal([Block, Submit], WorkflowRules.AllowedActions(Actor(Alex), Item(InProgress), Chart));
        Assert.Equal([Unblock], WorkflowRules.AllowedActions(Actor(Alex), Item(Blocked), Chart));
        Assert.Equal([Block, Submit], WorkflowRules.AllowedActions(Actor(Alex), Item(Returned), Chart));
        Assert.Empty(WorkflowRules.AllowedActions(Actor(Alex), Item(Submitted), Chart));
        Assert.Empty(WorkflowRules.AllowedActions(Actor(Alex), Item(Approved), Chart));
    }

    [Fact]
    public void AllowedActions_SubmittedItem_ReviewersAndManagersOnly()
    {
        Assert.Equal([Approve, Return], WorkflowRules.AllowedActions(Actor(Sam), Item(Submitted), Chart));
        Assert.Equal([Approve, Return], WorkflowRules.AllowedActions(Actor(Quinn), Item(Submitted), Chart));
        Assert.Empty(WorkflowRules.AllowedActions(Actor(Sam), Item(Submitted, submittedById: Sam), Chart));
    }

    [Fact]
    public void AllowedActions_ClosedMonthOrNotCovering_ListsNothing()
    {
        Assert.Empty(WorkflowRules.AllowedActions(Actor(Alex), Item(NotStarted, monthOpen: false), Chart));
        Assert.Empty(WorkflowRules.AllowedActions(Actor(Sam), Item(NotStarted), Chart));
    }

    [Fact]
    public void Apply_Block_SetsReasonStatusVersionAndEvent()
    {
        var item = Entity(InProgress);
        var decision = WorkflowRules.Decide(Actor(Alex), Item(InProgress), Chart, Block, "Waiting for the pending list").Value;

        var change = WorkflowRules.Apply(item, decision, Alex, Now);

        Assert.Equal(Blocked, item.Status);
        Assert.Equal("Waiting for the pending list", item.BlockedReason);
        Assert.Equal(4, item.Version);
        Assert.Same(change, Assert.Single(item.Events));
        Assert.Equal(WorkItemEventType.StatusChanged, change.Type);
        Assert.Equal((InProgress, Blocked), (change.FromStatus, change.ToStatus));
        Assert.Equal((Alex, (int?)null, Now), (change.ActorId, change.OnBehalfOfId, change.CreatedAt));
        Assert.Equal("Waiting for the pending list", change.Text);
    }

    [Fact]
    public void Apply_Unblock_ClearsReason()
    {
        var item = Entity(Blocked);
        item.BlockedReason = "Waiting";
        var decision = WorkflowRules.Decide(Actor(Alex), Item(Blocked), Chart, Unblock, null).Value;

        WorkflowRules.Apply(item, decision, Alex, Now);

        Assert.Equal(InProgress, item.Status);
        Assert.Null(item.BlockedReason);
    }

    [Fact]
    public void Apply_SubmitByCover_RecordsCoverAsSubmitter()
    {
        var item = Entity(InProgress);
        var decision = WorkflowRules.Decide(Actor(Blake, Alex), Item(InProgress), Chart, Submit, "All noted.").Value;

        var change = WorkflowRules.Apply(item, decision, Blake, Now);

        Assert.Equal(Submitted, item.Status);
        Assert.Equal(("All noted.", (int?)Blake, (DateTime?)Now), (item.Summary, item.SubmittedById, item.SubmittedAt));
        Assert.Equal((Blake, (int?)Alex), (change.ActorId, change.OnBehalfOfId));
    }

    [Fact]
    public void Apply_Approve_RecordsApprover()
    {
        var item = Entity(Submitted);
        var decision = WorkflowRules.Decide(Actor(Sam), Item(Submitted), Chart, Approve, null).Value;

        WorkflowRules.Apply(item, decision, Sam, Now);

        Assert.Equal(Approved, item.Status);
        Assert.Equal(((int?)Sam, (DateTime?)Now), (item.ApprovedById, item.ApprovedAt));
    }

    [Fact]
    public void Apply_Return_KeepsSummaryAndPutsCommentOnEvent()
    {
        var item = Entity(Submitted);
        item.Summary = "All noted.";
        var decision = WorkflowRules.Decide(Actor(Sam), Item(Submitted), Chart, Return, "Add follow-up dates.").Value;

        var change = WorkflowRules.Apply(item, decision, Sam, Now);

        Assert.Equal(Returned, item.Status);
        Assert.Equal("All noted.", item.Summary);
        Assert.Equal("Add follow-up dates.", change.Text);
    }

    private static ActorContext AllowedActorFor(WorkflowAction action) =>
        action is Approve or Return ? Actor(Sam) : Actor(Alex);

    private static ItemSnapshot Item(
        WorkItemStatus status, int ownerId = Alex, int? submittedById = null, bool monthOpen = true) =>
        new(status, ownerId, submittedById ?? (status is Submitted or Returned or Approved ? ownerId : null), monthOpen, DueDate);

    private static WorkItem Entity(WorkItemStatus status) =>
        new() { Status = status, AssigneeId = Alex, DueDate = DueDate, Version = 3 };
}
