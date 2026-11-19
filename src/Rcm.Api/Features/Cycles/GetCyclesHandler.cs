using Microsoft.EntityFrameworkCore;
using Rcm.Api.Data;
using Rcm.Api.Domain;

namespace Rcm.Api.Features.Cycles;

public sealed record CycleResponse(int Id, int Year, int Month, DateOnly DueDate, CycleStatus Status)
{
    public static CycleResponse From(ReviewCycle cycle) =>
        new(cycle.Id, cycle.Year, cycle.Month, cycle.DueDate, cycle.Status);
}

public sealed class GetCyclesHandler(AppDbContext db)
{
    public async Task<IReadOnlyList<CycleResponse>> HandleAsync(CancellationToken ct) =>
        await db.ReviewCycles
            .AsNoTracking()
            .OrderByDescending(c => c.Year)
            .ThenByDescending(c => c.Month)
            .Select(c => new CycleResponse(c.Id, c.Year, c.Month, c.DueDate, c.Status))
            .ToListAsync(ct);
}
