using Rcm.Api.Domain;
using Rcm.Api.Features.Cycles;

namespace Rcm.Api.Features.WorkItems;

public sealed record PersonRef(int Id, string Name);

public sealed record FacilityRef(int Id, string Code, string Name);

// Reviewer is empty when the owner reports to no one, which means any other manager reviews.
public sealed record WorkItemRow(
    int Id,
    FacilityRef Facility,
    PersonRef Owner,
    PersonRef? Reviewer,
    PersonRef Team,
    WorkItemStatus Status,
    DateOnly DueDate,
    IReadOnlyList<ItemFlag> Flags,
    long? ArTotalCents,
    long? ArOver90Cents,
    IReadOnlyList<WorkflowAction> AllowedActions,
    bool AwaitingMyReview,
    int Version);

public sealed record MyWorkResponse(
    CycleResponse Cycle,
    IReadOnlyList<WorkItemRow> NeedsAction,
    IReadOnlyList<WorkItemRow> WaitingOrDone);

public sealed record PersonSummary(PersonRef Person, int Open, int Submitted, int Approved);

public sealed record StatusCount(WorkItemStatus Status, int Count);

public sealed record TeamSummary(PersonRef Supervisor, int Items, int Approved, int Submitted, int Open, int Overdue);

// Progress and Teams are filled for managers only.
public sealed record OverviewResponse(
    CycleResponse Cycle,
    IReadOnlyList<WorkItemRow> WaitingForMyReview,
    IReadOnlyList<WorkItemRow> NeedsAttention,
    IReadOnlyList<PersonSummary> People,
    IReadOnlyList<WorkItemRow> Items,
    IReadOnlyList<StatusCount>? Progress,
    IReadOnlyList<TeamSummary>? Teams);

public sealed record EventResponse(
    int Id,
    WorkItemEventType Type,
    WorkItemStatus? FromStatus,
    WorkItemStatus? ToStatus,
    PersonRef Actor,
    PersonRef? OnBehalfOf,
    PersonRef? FromUser,
    PersonRef? ToUser,
    string? Text,
    DateTime CreatedAt);

public sealed record LastMonthResponse(WorkItemStatus Status, string? Summary);

// Events come newest first.
public sealed record WorkItemDetail(
    WorkItemRow Item,
    CycleResponse Cycle,
    string? Summary,
    string? BlockedReason,
    LastMonthResponse? LastMonth,
    IReadOnlyList<EventResponse> Events);

public sealed record TransitionRequest(WorkflowAction Action, string? Text, int Version);

public sealed record CommentRequest(string? Text);
