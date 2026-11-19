namespace Rcm.Api.Domain;

// How My work and Overview group items (docs/04).
public static class WorkLists
{
    // The order the manager's progress line uses.
    public static readonly IReadOnlyList<WorkItemStatus> ProgressOrder =
    [
        WorkItemStatus.Approved,
        WorkItemStatus.Submitted,
        WorkItemStatus.Returned,
        WorkItemStatus.InProgress,
        WorkItemStatus.Blocked,
        WorkItemStatus.NotStarted,
        WorkItemStatus.Incomplete,
    ];

    // Work the owner still has to do. My work lists these first; Overview counts them as open.
    public static bool NeedsAction(WorkItemStatus status) =>
        status is WorkItemStatus.Returned or WorkItemStatus.Blocked or WorkItemStatus.NotStarted or WorkItemStatus.InProgress;

    public static bool NeedsAttention(WorkItemStatus status, IReadOnlyCollection<ItemFlag> flags) =>
        status is WorkItemStatus.Blocked or WorkItemStatus.Returned
        || flags.Contains(ItemFlag.Overdue)
        || flags.Contains(ItemFlag.MissedLastMonth);
}
