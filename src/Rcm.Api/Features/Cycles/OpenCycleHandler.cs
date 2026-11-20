using Microsoft.EntityFrameworkCore;
using Rcm.Api.Auth;
using Rcm.Api.Data;
using Rcm.Api.Domain;

namespace Rcm.Api.Features.Cycles;

public sealed record OpenCycleRequest(DateOnly? DueDate);

// Opens the month after the latest one with one Not started item per active facility.
public sealed class OpenCycleHandler(AppDbContext db, BusinessCalendar calendar)
{
    public async Task<Result<CycleResponse>> HandleAsync(CurrentUser user, OpenCycleRequest request, CancellationToken ct)
    {
        var cycles = await db.ReviewCycles.AsNoTracking().ToListAsync(ct);
        var decision = CycleRules.CanOpen(
            user.Role, cycles.Select(c => c.Snapshot()).ToList(), calendar.Today, request.DueDate);
        if (!decision.IsSuccess)
        {
            return decision.Error;
        }

        var latest = cycles.MaxBy(c => (c.Year * 12) + c.Month);
        var previousItems = latest is null
            ? []
            : await db.WorkItems
                .Where(w => w.CycleId == latest.Id)
                .ToDictionaryAsync(w => w.FacilityId, w => w.Id, ct);
        var facilities = await db.Facilities
            .AsNoTracking()
            .Where(f => f.IsActive)
            .OrderBy(f => f.Id)
            .Select(f => new { f.Id, f.OwnerId })
            .ToListAsync(ct);

        var now = calendar.UtcNow;
        var cycle = new ReviewCycle
        {
            Year = decision.Value.Year,
            Month = decision.Value.Month,
            DueDate = request.DueDate!.Value,
            Status = CycleStatus.Open,
            OpenedById = user.Id,
            OpenedAt = now,
        };
        db.ReviewCycles.Add(cycle);
        db.WorkItems.AddRange(facilities.Select(f => CycleRules.NewItem(
            cycle,
            f.Id,
            f.OwnerId,
            previousItems.TryGetValue(f.Id, out var previousId) ? previousId : null,
            user.Id,
            now)));

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // The unique month and single open month indexes caught a second manager opening at the same time.
            return Error.Conflict("Another month was opened at the same time. Reload and try again.");
        }

        return CycleResponse.From(cycle);
    }
}
