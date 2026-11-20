using Microsoft.EntityFrameworkCore;
using Rcm.Api.Data;
using Rcm.Api.Domain;
using Rcm.Api.Features.Cycles;

namespace Rcm.Api.Features.WorkItems;

// Shared loading for the work item handlers. A month has one row per facility, so lists are built in memory.
public sealed class WorkItemReader(AppDbContext db)
{
    public static ItemSnapshot Snapshot(WorkItem item) => new(
        item.Status,
        item.AssigneeId,
        item.SubmittedById,
        item.Cycle!.Status == CycleStatus.Open,
        item.DueDate,
        item.PreviousItem?.Status);

    public static IReadOnlyList<WorkItemRow> ByArOver90(IEnumerable<WorkItemRow> rows) =>
        rows
            .OrderByDescending(r => r.ArOver90Cents.HasValue)
            .ThenByDescending(r => r.ArOver90Cents)
            .ThenBy(r => r.Facility.Code, StringComparer.Ordinal)
            .ToList();

    // No id means the open month, or the latest one when none is open.
    public async Task<ReviewCycle?> FindCycleAsync(int? cycleId, CancellationToken ct)
    {
        if (cycleId is { } id)
        {
            return await db.ReviewCycles.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, ct);
        }

        var cycles = await db.ReviewCycles.AsNoTracking().ToListAsync(ct);
        return cycles.FirstOrDefault(c => c.Status == CycleStatus.Open)
            ?? cycles.OrderByDescending(c => c.Year).ThenByDescending(c => c.Month).FirstOrDefault();
    }

    public async Task<List<WorkItem>> LoadItemsAsync(int cycleId, CancellationToken ct) =>
        await db.WorkItems
            .AsNoTracking()
            .Include(w => w.Cycle)
            .Include(w => w.Facility)
            .Include(w => w.PreviousItem)
            .Where(w => w.CycleId == cycleId)
            .ToListAsync(ct);

    public static WorkItemRow ToRow(WorkItem item, RequestContext context)
    {
        var snapshot = Snapshot(item);
        var facility = item.Facility!;
        var cover = context.CoverOf(item.AssigneeId);
        var coverMatters = ItemFlags.CoverMatters(snapshot);
        return new WorkItemRow(
            item.Id,
            new FacilityRef(facility.Id, facility.Code, facility.Name),
            context.Person(item.AssigneeId),
            context.OptionalPerson(ReviewerRules.ReviewerOf(item.AssigneeId, context.Org)),
            context.Person(context.Org.TeamOf(item.AssigneeId)),
            item.Status,
            item.DueDate,
            ItemFlags.For(snapshot, context.Today, cover),
            coverMatters ? context.OptionalPerson(cover.CoverId) : null,
            coverMatters && context.Actor.CoversToday.Contains(item.AssigneeId) ? context.Person(item.AssigneeId) : null,
            item.ArTotalCents,
            item.ArOver90Cents,
            WorkflowRules.AllowedActions(context.Actor, snapshot, context.Org),
            VisibilityRules.AwaitingMyReview(context.Actor, snapshot, context.Org),
            item.Version);
    }

    // Empty when the item does not exist or the actor cannot see it. Both read as not found.
    public async Task<WorkItemDetail?> LoadDetailAsync(int itemId, RequestContext context, CancellationToken ct)
    {
        var item = await db.WorkItems
            .AsNoTracking()
            .Include(w => w.Cycle)
            .Include(w => w.Facility)
            .Include(w => w.PreviousItem)
            .SingleOrDefaultAsync(w => w.Id == itemId, ct);
        if (item is null || !VisibilityRules.CanSee(context.Actor, item.AssigneeId, context.Org))
        {
            return null;
        }

        var events = await db.WorkItemEvents
            .AsNoTracking()
            .Where(e => e.WorkItemId == itemId)
            .OrderByDescending(e => e.CreatedAt)
            .ThenByDescending(e => e.Id)
            .ToListAsync(ct);

        return new WorkItemDetail(
            ToRow(item, context),
            CycleResponse.From(item.Cycle!),
            item.Summary,
            item.BlockedReason,
            item.PreviousItem is { } previous ? new LastMonthResponse(previous.Status, previous.Summary) : null,
            events.Select(e => new EventResponse(
                e.Id,
                e.Type,
                e.FromStatus,
                e.ToStatus,
                context.Person(e.ActorId),
                context.OptionalPerson(e.OnBehalfOfId),
                context.OptionalPerson(e.FromUserId),
                context.OptionalPerson(e.ToUserId),
                e.Text,
                e.CreatedAt)).ToList());
    }
}
