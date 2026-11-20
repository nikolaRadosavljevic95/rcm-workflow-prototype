using Microsoft.EntityFrameworkCore;
using Rcm.Api.Auth;
using Rcm.Api.Data;
using Rcm.Api.Domain;

namespace Rcm.Api.Features.WorkItems;

// A comment is an event only. It does not change the item, so Version stays as it is.
public sealed class AddCommentHandler(
    AppDbContext db, WorkItemReader reader, RequestContextLoader contexts, BusinessCalendar calendar)
{
    public async Task<Result<WorkItemDetail>> HandleAsync(
        CurrentUser user, int itemId, CommentRequest request, CancellationToken ct)
    {
        var context = await contexts.LoadAsync(user, ct);
        var item = await db.WorkItems
            .AsNoTracking()
            .Include(w => w.Cycle)
            .SingleOrDefaultAsync(w => w.Id == itemId, ct);
        if (item is null || !VisibilityRules.CanSee(context.Actor, item.AssigneeId, context.Org))
        {
            return Error.NotFound("Work item not found.");
        }

        var text = CommentRules.Check(item.Cycle!.Status == CycleStatus.Open, request.Text);
        if (!text.IsSuccess)
        {
            return text.Error;
        }

        db.WorkItemEvents.Add(new WorkItemEvent
        {
            WorkItemId = item.Id,
            Type = WorkItemEventType.Commented,
            ActorId = context.Actor.UserId,
            Text = text.Value,
            CreatedAt = calendar.UtcNow,
        });
        await db.SaveChangesAsync(ct);

        var detail = await reader.LoadDetailAsync(itemId, context, ct);
        return detail is null ? Error.NotFound("Work item not found.") : detail;
    }
}
