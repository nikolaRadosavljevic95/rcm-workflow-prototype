using Microsoft.EntityFrameworkCore;
using Rcm.Api.Auth;
using Rcm.Api.Data;
using Rcm.Api.Domain;

namespace Rcm.Api.Features.Me;

public sealed record MeResponse(int Id, string Name, Role Role, int? ReportsToId);

public sealed class GetMeHandler(AppDbContext db)
{
    public async Task<MeResponse?> HandleAsync(CurrentUser user, CancellationToken ct) =>
        await db.Users
            .AsNoTracking()
            .Where(u => u.Id == user.Id)
            .Select(u => new MeResponse(u.Id, u.Name, u.Role, u.ReportsToId))
            .SingleOrDefaultAsync(ct);
}
