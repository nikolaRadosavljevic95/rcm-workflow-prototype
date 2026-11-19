namespace Rcm.Api.Domain;

public class ReviewCycle
{
    public int Id { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public DateOnly DueDate { get; set; }
    public CycleStatus Status { get; set; }
    public int OpenedById { get; set; }
    public User? OpenedBy { get; set; }
    public DateTime OpenedAt { get; set; }
    public int? ClosedById { get; set; }
    public User? ClosedBy { get; set; }
    public DateTime? ClosedAt { get; set; }
}
