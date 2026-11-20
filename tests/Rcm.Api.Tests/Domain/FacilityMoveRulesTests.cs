using Rcm.Api.Domain;
using static Rcm.Api.Domain.WorkItemStatus;
using static Rcm.Api.Tests.TestSupport.TestOrg;

namespace Rcm.Api.Tests.Domain;

public class FacilityMoveRulesTests
{
    private static readonly DateTime Now = new(2026, 11, 19, 15, 0, 0, DateTimeKind.Utc);
    private static readonly OrgPerson DanaPerson = Chart.Find(Dana)!;

    [Theory]
    [InlineData(Role.Manager, CycleStatus.Open, true)]
    [InlineData(Role.Manager, CycleStatus.Closed, false)]
    [InlineData(Role.Supervisor, CycleStatus.Open, false)]
    public void ShowOnRows_ByRoleAndMonth_OnlyManagerInOpenMonth(Role role, CycleStatus month, bool expected)
    {
        Assert.Equal(expected, FacilityMoveRules.ShowOnRows(role, month));
    }

    [Fact]
    public void CanMove_ManagerToActiveBiller_TrimsReason()
    {
        var result = CanMove(newOwner: DanaPerson, reason: "  Workload balance  ");

        Assert.Equal(new FacilityMove(Dana, "Workload balance"), result.Value);
    }

    [Fact]
    public void CanMove_ToSupervisor_Succeeds()
    {
        Assert.True(CanMove(newOwner: Chart.Find(Casey)).IsSuccess);
    }

    [Theory]
    [InlineData(Role.Supervisor)]
    [InlineData(Role.Biller)]
    public void CanMove_NotManager_ReturnsForbidden(Role role)
    {
        Assert.Equal(ErrorKind.Forbidden, CanMove(role: role).Error?.Kind);
    }

    [Fact]
    public void CanMove_InactiveFacility_ReturnsInvalid()
    {
        Assert.Equal(ErrorKind.Invalid, CanMove(facilityActive: false).Error?.Kind);
    }

    [Fact]
    public void CanMove_NoNewOwner_ReturnsInvalid()
    {
        Assert.Equal(ErrorKind.Invalid, CanMove(withoutOwner: true).Error?.Kind);
    }

    [Fact]
    public void CanMove_ToManager_ReturnsInvalid()
    {
        Assert.Equal(ErrorKind.Invalid, CanMove(newOwner: Chart.Find(Quinn)).Error?.Kind);
    }

    [Fact]
    public void CanMove_ToInactiveUser_ReturnsInvalid()
    {
        Assert.Equal(ErrorKind.Invalid, CanMove(newOwner: DanaPerson with { IsActive = false }).Error?.Kind);
    }

    [Fact]
    public void CanMove_ToCurrentOwner_ReturnsInvalid()
    {
        var result = CanMove(currentOwnerId: Dana, newOwner: DanaPerson);

        Assert.Equal(ErrorKind.Invalid, result.Error?.Kind);
        Assert.Equal("Dana already owns this facility.", result.Error?.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CanMove_NoReason_ReturnsInvalid(string? reason)
    {
        var result = CanMove(reason: reason);

        Assert.Equal(ErrorKind.Invalid, result.Error?.Kind);
        Assert.Equal("A move needs a reason.", result.Error?.Message);
    }

    [Fact]
    public void CanMove_ReasonTooLong_ReturnsInvalid()
    {
        var result = CanMove(reason: new string('x', FacilityMoveRules.MaxReasonLength + 1));

        Assert.Equal(ErrorKind.Invalid, result.Error?.Kind);
    }

    [Fact]
    public void CanMove_ReasonAtLimit_Succeeds()
    {
        Assert.True(CanMove(reason: new string('x', FacilityMoveRules.MaxReasonLength)).IsSuccess);
    }

    [Fact]
    public void EligibleOwners_DemoOrg_ActiveBillersAndSupervisorsByName()
    {
        var people = new[] { Chart.Find(Morgan)!, Chart.Find(Sam)!, Chart.Find(Blake)!, Chart.Find(Alex)!, DanaPerson with { IsActive = false } };

        Assert.Equal(["Alex", "Blake", "Sam"], FacilityMoveRules.EligibleOwners(people).Select(p => p.Name));
    }

    [Theory]
    [InlineData(NotStarted, true)]
    [InlineData(InProgress, true)]
    [InlineData(Blocked, true)]
    [InlineData(Submitted, true)]
    [InlineData(Returned, true)]
    [InlineData(Approved, false)]
    [InlineData(Incomplete, false)]
    public void ItemMoves_EachStatus_OnlyOpenItemsMove(WorkItemStatus status, bool expected)
    {
        Assert.Equal(expected, FacilityMoveRules.ItemMoves(status));
    }

    [Fact]
    public void Apply_WithOpenItem_EndsAssignmentStartsNewAndMovesItem()
    {
        var (facility, current) = BlakesFacility();
        var item = new WorkItem { AssigneeId = Blake, Status = Submitted, SubmittedById = Blake, Version = 4 };

        var result = FacilityMoveRules.Apply(facility, current, new FacilityMove(Dana, "Workload balance"), item, Morgan, Now);

        Assert.Equal(Now, current.EndedAt);
        Assert.Equal((7, Dana, Now, (DateTime?)null, "Workload balance", Morgan), (result.Started.FacilityId, result.Started.UserId, result.Started.StartedAt, result.Started.EndedAt, result.Started.Reason, result.Started.ChangedById));
        Assert.Equal(Dana, facility.OwnerId);
        Assert.Equal((Dana, Submitted, Blake, 5), (item.AssigneeId, item.Status, item.SubmittedById, item.Version));
        var moved = Assert.Single(item.Events);
        Assert.Same(moved, result.MovedItemEvent);
        Assert.Equal((WorkItemEventType.Reassigned, Blake, Dana, Morgan, "Workload balance", Now), (moved.Type, moved.FromUserId, moved.ToUserId, moved.ActorId, moved.Text, moved.CreatedAt));
    }

    [Theory]
    [InlineData(Approved)]
    [InlineData(Incomplete)]
    public void Apply_FinalItem_ItemStaysAndOwnershipMoves(WorkItemStatus status)
    {
        var (facility, current) = BlakesFacility();
        var item = new WorkItem { AssigneeId = Blake, Status = status, Version = 2 };

        var result = FacilityMoveRules.Apply(facility, current, new FacilityMove(Dana, "Workload balance"), item, Morgan, Now);

        Assert.Null(result.MovedItemEvent);
        Assert.Equal((Blake, 2), (item.AssigneeId, item.Version));
        Assert.Empty(item.Events);
        Assert.Equal(Dana, facility.OwnerId);
    }

    [Fact]
    public void Apply_NoItem_OnlyOwnershipMoves()
    {
        var (facility, current) = BlakesFacility();

        var result = FacilityMoveRules.Apply(facility, current, new FacilityMove(Dana, "Workload balance"), null, Morgan, Now);

        Assert.Null(result.MovedItemEvent);
        Assert.Equal((Dana, Now), (facility.OwnerId, current.EndedAt));
    }

    private static Result<FacilityMove> CanMove(
        Role role = Role.Manager,
        bool facilityActive = true,
        int currentOwnerId = Blake,
        OrgPerson? newOwner = null,
        string? reason = "Workload balance",
        bool withoutOwner = false) =>
        FacilityMoveRules.CanMove(role, facilityActive, currentOwnerId, withoutOwner ? null : newOwner ?? DanaPerson, reason);

    private static (Facility Facility, FacilityAssignment Current) BlakesFacility() =>
        (new Facility { Id = 7, Code = "F07", Name = "Facility 07", OwnerId = Blake },
         new FacilityAssignment { FacilityId = 7, UserId = Blake, Reason = "Initial assignment", ChangedById = Morgan });
}
