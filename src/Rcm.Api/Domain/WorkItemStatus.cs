namespace Rcm.Api.Domain;

public enum WorkItemStatus
{
    NotStarted,
    InProgress,
    Blocked,
    Submitted,
    Returned,
    Approved,
    Incomplete,
}

public static class WorkItemStatusExtensions
{
    // Approved and Incomplete never change again in v1: closing a month and moving a facility leave them alone.
    public static bool IsFinal(this WorkItemStatus status) =>
        status is WorkItemStatus.Approved or WorkItemStatus.Incomplete;
}
