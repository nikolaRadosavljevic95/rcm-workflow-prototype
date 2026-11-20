using Microsoft.EntityFrameworkCore;
using Rcm.Api.Auth;
using Rcm.Api.Data;
using Rcm.Api.Domain;

namespace Rcm.Api.Features;

public sealed record PersonRef(int Id, string Name);

// What the rules need to know about the caller and about today, loaded once per request.
public sealed record RequestContext(DateOnly Today, OrgChart Org, ActorContext Actor, IReadOnlyList<AbsenceSnapshot> AbsencesToday)
{
    public CoverState CoverOf(int ownerId) => CoverRules.StateOf(ownerId, Today, AbsencesToday, Org);

    public PersonRef Person(int userId) => new(userId, Org.Find(userId)?.Name ?? $"User {userId}");

    public PersonRef? OptionalPerson(int? userId) => userId is { } id ? Person(id) : null;
}

public sealed class RequestContextLoader(AppDbContext db, BusinessCalendar calendar)
{
    public async Task<RequestContext> LoadAsync(CurrentUser user, CancellationToken ct)
    {
        var today = calendar.Today;
        var people = await db.Users
            .AsNoTracking()
            .Select(u => new OrgPerson(u.Id, u.Name, u.Role, u.ReportsToId, u.IsActive))
            .ToListAsync(ct);
        var org = new OrgChart(people);
        var absencesToday = await db.Absences
            .AsNoTracking()
            .Where(a => a.StartDate <= today && a.EndDate >= today)
            .Select(a => new AbsenceSnapshot(a.Id, a.UserId, a.StartDate, a.EndDate, a.CoverUserId))
            .ToListAsync(ct);

        var actor = new ActorContext(user.Id, user.Role, CoverRules.CoversToday(user.Id, today, absencesToday, org));
        return new RequestContext(today, org, actor, absencesToday);
    }
}
