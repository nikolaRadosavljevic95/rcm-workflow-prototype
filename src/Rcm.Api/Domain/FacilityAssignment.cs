namespace Rcm.Api.Domain;

public class FacilityAssignment
{
    public int Id { get; set; }
    public int FacilityId { get; set; }
    public Facility? Facility { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public required string Reason { get; set; }
    public int ChangedById { get; set; }
    public User? ChangedBy { get; set; }
}
