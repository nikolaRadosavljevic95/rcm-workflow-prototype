using Rcm.Api.Domain;

namespace Rcm.Api.Tests.TestSupport;

// The demo org chart for rule tests, without a database.
public static class TestOrg
{
    public const int Morgan = 1, Quinn = 2, Sam = 3, Taylor = 4, Jordan = 5, Casey = 6;
    public const int Alex = 7, Blake = 8, Charlie = 9, Dana = 10, Emery = 11;

    public static readonly OrgChart Chart = new(
    [
        new OrgPerson(Morgan, "Morgan", Role.Manager, null),
        new OrgPerson(Quinn, "Quinn", Role.Manager, null),
        new OrgPerson(Sam, "Sam", Role.Supervisor, Morgan),
        new OrgPerson(Taylor, "Taylor", Role.Supervisor, Morgan),
        new OrgPerson(Jordan, "Jordan", Role.Supervisor, Quinn),
        new OrgPerson(Casey, "Casey", Role.Supervisor, Quinn),
        new OrgPerson(Alex, "Alex", Role.Biller, Sam),
        new OrgPerson(Blake, "Blake", Role.Biller, Sam),
        new OrgPerson(Charlie, "Charlie", Role.Biller, Taylor),
        new OrgPerson(Dana, "Dana", Role.Biller, Jordan),
        new OrgPerson(Emery, "Emery", Role.Biller, Casey),
    ]);

    public static ActorContext Actor(int userId, params int[] coversToday) =>
        new(userId, Chart.Find(userId)!.Role, coversToday.ToHashSet());
}
