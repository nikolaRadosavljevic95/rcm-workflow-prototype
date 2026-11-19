using Rcm.Api.Data;
using Rcm.Api.Domain;

namespace Rcm.Api.Features.Demo;

public sealed class ResetDemoDataHandler(AppDbContext db, BusinessCalendar calendar)
{
    public async Task HandleAsync(CancellationToken ct)
    {
        await db.Database.EnsureDeletedAsync(ct);
        await db.Database.EnsureCreatedAsync(ct);
        await SeedData.SeedAsync(db, calendar, ct);
    }
}
