using Rcm.Api.Domain;

namespace Rcm.Api.Tests.Domain;

public class WorkListsTests
{
    [Theory]
    [InlineData(WorkItemStatus.Returned, true)]
    [InlineData(WorkItemStatus.Blocked, true)]
    [InlineData(WorkItemStatus.NotStarted, true)]
    [InlineData(WorkItemStatus.InProgress, true)]
    [InlineData(WorkItemStatus.Submitted, false)]
    [InlineData(WorkItemStatus.Approved, false)]
    [InlineData(WorkItemStatus.Incomplete, false)]
    public void NeedsAction_Status_OwnerStillHasWork(WorkItemStatus status, bool expected)
    {
        Assert.Equal(expected, WorkLists.NeedsAction(status));
    }

    [Theory]
    [InlineData(WorkItemStatus.Blocked)]
    [InlineData(WorkItemStatus.Returned)]
    public void NeedsAttention_BlockedOrReturned_IsTrueWithoutFlags(WorkItemStatus status)
    {
        Assert.True(WorkLists.NeedsAttention(status, []));
    }

    [Theory]
    [InlineData(ItemFlag.Overdue)]
    [InlineData(ItemFlag.NeedsCover)]
    [InlineData(ItemFlag.MissedLastMonth)]
    public void NeedsAttention_FlaggedItem_IsTrue(ItemFlag flag)
    {
        Assert.True(WorkLists.NeedsAttention(WorkItemStatus.InProgress, [flag]));
    }

    [Fact]
    public void NeedsAttention_OnTrackItem_IsFalse()
    {
        Assert.False(WorkLists.NeedsAttention(WorkItemStatus.InProgress, []));
        Assert.False(WorkLists.NeedsAttention(WorkItemStatus.Submitted, []));
        Assert.False(WorkLists.NeedsAttention(WorkItemStatus.InProgress, [ItemFlag.Covered]));
    }
}
