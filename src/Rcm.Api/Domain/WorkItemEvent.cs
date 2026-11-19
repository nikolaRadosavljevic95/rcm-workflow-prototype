namespace Rcm.Api.Domain;

public class WorkItemEvent
{
    public int Id { get; set; }
    public int WorkItemId { get; set; }
    public WorkItem? WorkItem { get; set; }
    public WorkItemEventType Type { get; set; }
    public WorkItemStatus? FromStatus { get; set; }
    public WorkItemStatus? ToStatus { get; set; }
    public int? FromUserId { get; set; }
    public User? FromUser { get; set; }
    public int? ToUserId { get; set; }
    public User? ToUser { get; set; }
    public int ActorId { get; set; }
    public User? Actor { get; set; }
    public int? OnBehalfOfId { get; set; }
    public User? OnBehalfOf { get; set; }
    public string? Text { get; set; }
    public DateTime CreatedAt { get; set; }
}
