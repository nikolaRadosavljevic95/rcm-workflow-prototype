namespace Rcm.Api.Domain;

public class User
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Email { get; set; }
    public Role Role { get; set; }
    public int? ReportsToId { get; set; }
    public User? ReportsTo { get; set; }
    public bool IsActive { get; set; } = true;
}
