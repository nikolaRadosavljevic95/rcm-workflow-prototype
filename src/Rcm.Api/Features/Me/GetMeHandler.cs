using Microsoft.EntityFrameworkCore;
using Rcm.Api.Auth;
using Rcm.Api.Data;
using Rcm.Api.Domain;

namespace Rcm.Api.Features.Me;

// CoveringFor lists the people whose work the user may do today as their cover.
public sealed record MeResponse(int Id, string Name, Role Role, int? ReportsToId, IReadOnlyList<PersonRef> CoveringFor);

public sealed class GetMeHandler(AppDbContext db, RequestContextLoader contexts)
{
    public async Task<MeResponse?> HandleAsync(CurrentUser user, CancellationToken ct)
    {
        var me = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == user.Id, ct);
        if (me is null)
        {
            return null;
        }

        var context = await contexts.LoadAsync(user, ct);
        var coveringFor = context.Actor.CoversToday
            .Select(context.Person)
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .ToList();
        return new MeResponse(me.Id, me.Name, me.Role, me.ReportsToId, coveringFor);
    }
}
