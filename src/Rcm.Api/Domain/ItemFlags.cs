namespace Rcm.Api.Domain;

public enum ItemFlag
{
    Overdue,
    NeedsCover,
    Covered,
    MissedLastMonth,
}

// Flags are computed when read. They are not statuses: an item can be overdue in any open status.
public static class ItemFlags
{
    public static IReadOnlyList<ItemFlag> For(ItemSnapshot item, DateOnly today, CoverState? cover = null)
    {
        var flags = new List<ItemFlag>();
        if (IsOverdue(item, today))
        {
            flags.Add(ItemFlag.Overdue);
        }

        if (CoverMatters(item))
        {
            switch (cover?.Status)
            {
                case CoverStatus.NeedsCover:
                    flags.Add(ItemFlag.NeedsCover);
                    break;
                case CoverStatus.Covered:
                    flags.Add(ItemFlag.Covered);
                    break;
            }
        }

        if (item.PreviousStatus == WorkItemStatus.Incomplete)
        {
            flags.Add(ItemFlag.MissedLastMonth);
        }

        return flags;
    }

    public static bool IsOverdue(ItemSnapshot item, DateOnly today) =>
        today > item.DueDate && IsOpen(item.Status);

    // Who covers the owner only matters while someone can still act on the item.
    public static bool CoverMatters(ItemSnapshot item) => item.MonthOpen && IsOpen(item.Status);

    private static bool IsOpen(WorkItemStatus status) =>
        status is not (WorkItemStatus.Approved or WorkItemStatus.Incomplete);
}
