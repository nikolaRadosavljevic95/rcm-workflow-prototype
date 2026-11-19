namespace Rcm.Api.Domain;

public enum ItemFlag
{
    Overdue,
    MissedLastMonth,
}

// Flags are computed when read. They are not statuses: an item can be overdue in any open status.
public static class ItemFlags
{
    public static IReadOnlyList<ItemFlag> For(ItemSnapshot item, DateOnly today)
    {
        var flags = new List<ItemFlag>();
        if (IsOverdue(item, today))
        {
            flags.Add(ItemFlag.Overdue);
        }

        if (item.PreviousStatus == WorkItemStatus.Incomplete)
        {
            flags.Add(ItemFlag.MissedLastMonth);
        }

        return flags;
    }

    public static bool IsOverdue(ItemSnapshot item, DateOnly today) =>
        today > item.DueDate && item.Status is not (WorkItemStatus.Approved or WorkItemStatus.Incomplete);
}
