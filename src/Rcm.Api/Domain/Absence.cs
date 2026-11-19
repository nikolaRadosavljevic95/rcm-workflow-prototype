namespace Rcm.Api.Domain;

public class Absence
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }

    // Both dates are inclusive.
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }

    // Empty means the absence needs a cover.
    public int? CoverUserId { get; set; }
    public User? CoverUser { get; set; }
    public string? Note { get; set; }
    public int CreatedById { get; set; }
    public User? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}
