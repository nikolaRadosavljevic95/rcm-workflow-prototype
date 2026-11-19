namespace Rcm.Api.Domain;

public sealed record OrgPerson(int Id, string Name, Role Role, int? ReportsToId);

// Reporting lines as they are now. Reviewers and teams are read from here when someone acts, never stored.
public sealed class OrgChart
{
    private readonly Dictionary<int, OrgPerson> people;

    public OrgChart(IEnumerable<OrgPerson> people) => this.people = people.ToDictionary(p => p.Id);

    public OrgPerson? Find(int userId) => people.GetValueOrDefault(userId);

    public int? ManagerOf(int userId) => Find(userId)?.ReportsToId;

    // A biller's team is their supervisor's. Anyone else heads their own.
    public int TeamOf(int ownerId) =>
        Find(ownerId) is { Role: Role.Biller, ReportsToId: { } supervisorId } ? supervisorId : ownerId;
}
