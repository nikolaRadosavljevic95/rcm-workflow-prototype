using Microsoft.EntityFrameworkCore;
using Rcm.Api.Auth;
using Rcm.Api.Data;
using Rcm.Api.Domain;
using Rcm.Api.Features.WorkItems;

namespace Rcm.Api.Features.Cycles;

public sealed record ClosePreviewRow(int Id, FacilityRef Facility, WorkItemStatus Status, PersonRef Owner);

// Items lists what becomes Incomplete when the month closes, by facility code.
public sealed record ClosePreviewResponse(CycleResponse Cycle, IReadOnlyList<ClosePreviewRow> Items);

public sealed class CloseCycleHandler(AppDbContext db, BusinessCalendar calendar)
{
    public async Task<Result<ClosePreviewResponse>> PreviewAsync(CurrentUser user, int cycleId, CancellationToken ct)
    {
        var cycle = await db.ReviewCycles.AsNoTracking().SingleOrDefaultAsync(c => c.Id == cycleId, ct);
        if (cycle is null)
        {
            return Error.NotFound("Month not found.");
        }

        var decision = CycleRules.CanClose(user.Role, cycle.Snapshot());
        if (!decision.IsSuccess)
        {
            return decision.Error;
        }

        var items = await db.WorkItems
            .AsNoTracking()
            .Include(w => w.Facility)
            .Include(w => w.Assignee)
            .Where(w => w.CycleId == cycleId)
            .ToListAsync(ct);

        return new ClosePreviewResponse(
            CycleResponse.From(cycle),
            items
                .Where(w => CycleRules.ClosesAsIncomplete(w.Status))
                .OrderBy(w => w.Facility!.Code, StringComparer.Ordinal)
                .Select(w => new ClosePreviewRow(
                    w.Id,
                    new FacilityRef(w.FacilityId, w.Facility!.Code, w.Facility.Name),
                    w.Status,
                    new PersonRef(w.AssigneeId, w.Assignee!.Name)))
                .ToList());
    }

    // One SaveChanges, so the items and the month change in one transaction.
    public async Task<Result<CycleResponse>> CloseAsync(CurrentUser user, int cycleId, CancellationToken ct)
    {
        var cycle = await db.ReviewCycles.SingleOrDefaultAsync(c => c.Id == cycleId, ct);
        if (cycle is null)
        {
            return Error.NotFound("Month not found.");
        }

        var decision = CycleRules.CanClose(user.Role, cycle.Snapshot());
        if (!decision.IsSuccess)
        {
            return decision.Error;
        }

        var now = calendar.UtcNow;
        var items = await db.WorkItems.Where(w => w.CycleId == cycleId).ToListAsync(ct);
        foreach (var item in items.Where(w => CycleRules.ClosesAsIncomplete(w.Status)))
        {
            CycleRules.CloseItem(item, user.Id, now);
        }

        cycle.Status = CycleStatus.Closed;
        cycle.ClosedById = user.Id;
        cycle.ClosedAt = now;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // An item changed between our read and our write. Nothing was saved.
            return Error.Conflict("An item changed while the month was closing. Check the preview again.");
        }

        return CycleResponse.From(cycle);
    }
}
