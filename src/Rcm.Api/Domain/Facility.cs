namespace Rcm.Api.Domain;

public class Facility
{
    public int Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public int OwnerId { get; set; }
    public User? Owner { get; set; }
    public bool IsActive { get; set; } = true;
}
