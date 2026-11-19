using Rcm.Api.Domain;
using static Rcm.Api.Tests.TestSupport.TestOrg;

namespace Rcm.Api.Tests.Domain;

public class VisibilityRulesTests
{
    private static readonly DateOnly DueDate = new(2026, 11, 18);

    [Theory]
    [InlineData(Role.Biller, false)]
    [InlineData(Role.Supervisor, true)]
    [InlineData(Role.Manager, true)]
    public void HasOverview_Role_SupervisorsAndManagersOnly(Role role, bool expected)
    {
        Assert.Equal(expected, VisibilityRules.HasOverview(role));
    }

    [Fact]
    public void CanSee_Manager_SeesEveryItem()
    {
        Assert.All([Alex, Charlie, Dana, Casey, Morgan], owner => Assert.True(VisibilityRules.CanSee(Actor(Quinn), owner, Chart)));
    }

    [Fact]
    public void CanSee_Supervisor_SeesOwnAndDirectReportsOnly()
    {
        Assert.True(VisibilityRules.CanSee(Actor(Casey), Casey, Chart));
        Assert.True(VisibilityRules.CanSee(Actor(Casey), Emery, Chart));
        Assert.False(VisibilityRules.CanSee(Actor(Casey), Dana, Chart));
        Assert.False(VisibilityRules.CanSee(Actor(Casey), Jordan, Chart));
    }

    [Fact]
    public void CanSee_SupervisorCoveringSupervisor_SeesTheirItemsAndReports()
    {
        var sam = Actor(Sam, Taylor);

        Assert.True(VisibilityRules.CanSee(sam, Taylor, Chart));
        Assert.True(VisibilityRules.CanSee(sam, Charlie, Chart));
        Assert.False(VisibilityRules.CanSee(sam, Dana, Chart));
    }

    [Fact]
    public void CanSee_Biller_SeesOwnAndCoveredOnly()
    {
        Assert.True(VisibilityRules.CanSee(Actor(Alex), Alex, Chart));
        Assert.False(VisibilityRules.CanSee(Actor(Alex), Blake, Chart));
        Assert.True(VisibilityRules.CanSee(Actor(Alex, Blake), Blake, Chart));
        Assert.False(VisibilityRules.CanSee(Actor(Alex, Blake), Charlie, Chart));
    }

    [Fact]
    public void InMyWork_OwnOrCovered_IsIncluded()
    {
        Assert.True(VisibilityRules.InMyWork(Actor(Alex), Alex));
        Assert.True(VisibilityRules.InMyWork(Actor(Alex, Blake), Blake));
        Assert.False(VisibilityRules.InMyWork(Actor(Sam), Alex));
    }

    [Fact]
    public void AwaitingMyReview_ReviewerOrCoverOfSubmittedItem_IsTrue()
    {
        var item = Submitted(owner: Alex, submittedBy: Alex);

        Assert.True(VisibilityRules.AwaitingMyReview(Actor(Sam), item, Chart));
        Assert.True(VisibilityRules.AwaitingMyReview(Actor(Taylor, Sam), item, Chart));
    }

    [Fact]
    public void AwaitingMyReview_ManagerWhoCouldApprove_IsFalse()
    {
        Assert.False(VisibilityRules.AwaitingMyReview(Actor(Morgan), Submitted(owner: Alex, submittedBy: Alex), Chart));
    }

    [Fact]
    public void AwaitingMyReview_ReviewerWhoSubmitted_IsFalse()
    {
        Assert.False(VisibilityRules.AwaitingMyReview(Actor(Sam, Alex), Submitted(owner: Alex, submittedBy: Sam), Chart));
    }

    [Fact]
    public void AwaitingMyReview_NotSubmitted_IsFalse()
    {
        var item = new ItemSnapshot(WorkItemStatus.InProgress, Alex, null, true, DueDate);

        Assert.False(VisibilityRules.AwaitingMyReview(Actor(Sam), item, Chart));
    }

    [Fact]
    public void AwaitingMyReview_OwnerReportingToNoOne_OtherManagers()
    {
        var item = Submitted(owner: Morgan, submittedBy: Morgan);

        Assert.True(VisibilityRules.AwaitingMyReview(Actor(Quinn), item, Chart));
        Assert.False(VisibilityRules.AwaitingMyReview(Actor(Morgan), item, Chart));
    }

    private static ItemSnapshot Submitted(int owner, int submittedBy) =>
        new(WorkItemStatus.Submitted, owner, submittedBy, true, DueDate);
}
