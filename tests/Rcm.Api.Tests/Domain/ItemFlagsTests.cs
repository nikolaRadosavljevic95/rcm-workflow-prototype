using Rcm.Api.Domain;
using Rcm.Api.Tests.TestSupport;

namespace Rcm.Api.Tests.Domain;

public class ItemFlagsTests
{
    private static readonly DateOnly DueDate = new(2026, 11, 18);

    [Fact]
    public void For_DueToday_IsNotOverdue()
    {
        Assert.Empty(ItemFlags.For(Item(WorkItemStatus.InProgress), DueDate));
    }

    [Fact]
    public void For_DueYesterday_IsOverdue()
    {
        Assert.Equal([ItemFlag.Overdue], ItemFlags.For(Item(WorkItemStatus.InProgress), DueDate.AddDays(1)));
    }

    [Theory]
    [InlineData(WorkItemStatus.NotStarted, true)]
    [InlineData(WorkItemStatus.InProgress, true)]
    [InlineData(WorkItemStatus.Blocked, true)]
    [InlineData(WorkItemStatus.Submitted, true)]
    [InlineData(WorkItemStatus.Returned, true)]
    [InlineData(WorkItemStatus.Approved, false)]
    [InlineData(WorkItemStatus.Incomplete, false)]
    public void IsOverdue_PastDueByStatus_OnlyUnfinishedItems(WorkItemStatus status, bool expected)
    {
        Assert.Equal(expected, ItemFlags.IsOverdue(Item(status), DueDate.AddDays(3)));
    }

    [Fact]
    public void For_LateEveningInNewYork_UsesBusinessDateNotUtc()
    {
        // 23:30 on the due date in New York is already the next day in UTC.
        var calendar = new BusinessCalendar(
            new FixedTimeProvider(new DateTimeOffset(2026, 11, 19, 4, 30, 0, TimeSpan.Zero)), SqliteTestDatabase.NewYork);

        Assert.Empty(ItemFlags.For(Item(WorkItemStatus.InProgress), calendar.Today));
    }

    [Fact]
    public void For_LastMonthIncomplete_IsMissedLastMonth()
    {
        var item = Item(WorkItemStatus.NotStarted) with { PreviousStatus = WorkItemStatus.Incomplete };

        Assert.Equal([ItemFlag.MissedLastMonth], ItemFlags.For(item, DueDate));
    }

    [Fact]
    public void For_LastMonthApproved_IsNotMissedLastMonth()
    {
        var item = Item(WorkItemStatus.NotStarted) with { PreviousStatus = WorkItemStatus.Approved };

        Assert.Empty(ItemFlags.For(item, DueDate));
    }

    private static ItemSnapshot Item(WorkItemStatus status) => new(status, TestOrg.Alex, null, true, DueDate);
}
