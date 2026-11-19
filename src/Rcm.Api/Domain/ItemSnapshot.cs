namespace Rcm.Api.Domain;

// The facts about a work item that the rules read.
public sealed record ItemSnapshot(
    WorkItemStatus Status,
    int OwnerId,
    int? SubmittedById,
    bool MonthOpen,
    DateOnly DueDate,
    WorkItemStatus? PreviousStatus = null);
