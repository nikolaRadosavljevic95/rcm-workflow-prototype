using Microsoft.EntityFrameworkCore;
using Rcm.Api.Auth;
using Rcm.Api.Data;
using Rcm.Api.Domain;

namespace Rcm.Api.Features.WorkItems;

public sealed class TransitionWorkItemHandler(
    AppDbContext db, WorkItemReader reader, RequestContextLoader contexts, BusinessCalendar calendar)
{
    private static readonly Error Stale = Error.Conflict("Someone changed this item. Reload it and try again.");

    public async Task<Result<WorkItemDetail>> HandleAsync(
        CurrentUser user, int itemId, TransitionRequest request, CancellationToken ct)
    {
        var context = await contexts.LoadAsync(user, ct);
        var item = await db.WorkItems
            .Include(w => w.Cycle)
            .Include(w => w.PreviousItem)
            .SingleOrDefaultAsync(w => w.Id == itemId, ct);
        if (item is null || !VisibilityRules.CanSee(context.Actor, item.AssigneeId, context.Org))
        {
            return Error.NotFound("Work item not found.");
        }

        if (item.Version != request.Version)
        {
            return Stale;
        }

        var decision = WorkflowRules.Decide(
            context.Actor, WorkItemReader.Snapshot(item), context.Org, request.Action, request.Text);
        if (!decision.IsSuccess)
        {
            return decision.Error;
        }

        WorkflowRules.Apply(item, decision.Value, context.Actor.UserId, calendar.UtcNow);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another request saved between our read and our write.
            return Stale;
        }

        var detail = await reader.LoadDetailAsync(itemId, context, ct);
        return detail is null ? Error.NotFound("Work item not found.") : detail;
    }
}
