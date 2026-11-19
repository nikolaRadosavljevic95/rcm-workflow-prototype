namespace Rcm.Api.Domain;

public class WorkItem
{
    public int Id { get; set; }
    public int CycleId { get; set; }
    public ReviewCycle? Cycle { get; set; }
    public int FacilityId { get; set; }
    public Facility? Facility { get; set; }
    public int AssigneeId { get; set; }
    public User? Assignee { get; set; }
    public WorkItemStatus Status { get; set; }
    public DateOnly DueDate { get; set; }
    public string? BlockedReason { get; set; }
    public string? Summary { get; set; }
    public int? SubmittedById { get; set; }
    public User? SubmittedBy { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public int? ApprovedById { get; set; }
    public User? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public int? PreviousItemId { get; set; }
    public WorkItem? PreviousItem { get; set; }

    // Empty until a balance import fills them.
    public long? ArTotalCents { get; set; }
    public long? ArOver90Cents { get; set; }

    public int Version { get; set; } = 1;
    public List<WorkItemEvent> Events { get; set; } = [];
}
