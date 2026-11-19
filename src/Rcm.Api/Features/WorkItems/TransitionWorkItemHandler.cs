using Microsoft.EntityFrameworkCore;
using Rcm.Api.Auth;
using Rcm.Api.Data;
using Rcm.Api.Domain;

namespace Rcm.Api.Features.WorkItems;

public sealed class TransitionWorkItemHandler(AppDbContext db, WorkItemReader reader, BusinessCalendar calendar)
{
    private static readonly Error Stale = Error.Conflict("Someone changed this item. Reload it and try again.");

    public async Task<Result<WorkItemDetail>> HandleAsync(
        CurrentUser user, int itemId, TransitionRequest request, CancellationToken ct)
    {
        var org = await reader.LoadOrgAsync(ct);
        var actor = WorkItemReader.ActorFor(user);
        var item = await db.WorkItems
            .Include(w => w.Cycle)
            .Include(w => w.PreviousItem)
            .SingleOrDefaultAsync(w => w.Id == itemId, ct);
        if (item is null || !VisibilityRules.CanSee(actor, item.AssigneeId, org))
        {
            return Error.NotFound("Work item not found.");
        }

        if (item.Version != request.Version)
        {
            return Stale;
        }

        var decision = WorkflowRules.Decide(actor, WorkItemReader.Snapshot(item), org, request.Action, request.Text);
        if (!decision.IsSuccess)
        {
            return decision.Error;
        }

        WorkflowRules.Apply(item, decision.Value, actor.UserId, calendar.UtcNow);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another request saved between our read and our write.
            return Stale;
        }

        var detail = await reader.LoadDetailAsync(itemId, actor, org, ct);
        return detail is null ? Error.NotFound("Work item not found.") : detail;
    }
}
