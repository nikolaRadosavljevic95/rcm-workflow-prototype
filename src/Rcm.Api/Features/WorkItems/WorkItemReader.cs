using Microsoft.EntityFrameworkCore;
using Rcm.Api.Auth;
using Rcm.Api.Data;
using Rcm.Api.Domain;
using Rcm.Api.Features.Cycles;

namespace Rcm.Api.Features.WorkItems;

// Shared loading for the work item handlers. A month has one row per facility, so lists are built in memory.
public sealed class WorkItemReader(AppDbContext db, BusinessCalendar calendar)
{
    private static readonly IReadOnlySet<int> NobodyCovered = new HashSet<int>();

    // Covers are not resolved from absences yet, so an actor acts only for themselves.
    public static ActorContext ActorFor(CurrentUser user) => new(user.Id, user.Role, NobodyCovered);

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

    public async Task<OrgChart> LoadOrgAsync(CancellationToken ct)
    {
        var people = await db.Users
            .AsNoTracking()
            .Select(u => new OrgPerson(u.Id, u.Name, u.Role, u.ReportsToId))
            .ToListAsync(ct);
        return new OrgChart(people);
    }

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

    public WorkItemRow ToRow(WorkItem item, ActorContext actor, OrgChart org)
    {
        var snapshot = Snapshot(item);
        var facility = item.Facility!;
        return new WorkItemRow(
            item.Id,
            new FacilityRef(facility.Id, facility.Code, facility.Name),
            Person(item.AssigneeId, org),
            ReviewerRules.ReviewerOf(item.AssigneeId, org) is { } reviewerId ? Person(reviewerId, org) : null,
            Person(org.TeamOf(item.AssigneeId), org),
            item.Status,
            item.DueDate,
            ItemFlags.For(snapshot, calendar.Today),
            item.ArTotalCents,
            item.ArOver90Cents,
            WorkflowRules.AllowedActions(actor, snapshot, org),
            VisibilityRules.AwaitingMyReview(actor, snapshot, org),
            item.Version);
    }

    // Empty when the item does not exist or the actor cannot see it. Both read as not found.
    public async Task<WorkItemDetail?> LoadDetailAsync(int itemId, ActorContext actor, OrgChart org, CancellationToken ct)
    {
        var item = await db.WorkItems
            .AsNoTracking()
            .Include(w => w.Cycle)
            .Include(w => w.Facility)
            .Include(w => w.PreviousItem)
            .SingleOrDefaultAsync(w => w.Id == itemId, ct);
        if (item is null || !VisibilityRules.CanSee(actor, item.AssigneeId, org))
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
            ToRow(item, actor, org),
            CycleResponse.From(item.Cycle!),
            item.Summary,
            item.BlockedReason,
            item.PreviousItem is { } previous ? new LastMonthResponse(previous.Status, previous.Summary) : null,
            events.Select(e => new EventResponse(
                e.Id,
                e.Type,
                e.FromStatus,
                e.ToStatus,
                Person(e.ActorId, org),
                OptionalPerson(e.OnBehalfOfId, org),
                OptionalPerson(e.FromUserId, org),
                OptionalPerson(e.ToUserId, org),
                e.Text,
                e.CreatedAt)).ToList());
    }

    private static PersonRef Person(int userId, OrgChart org) => new(userId, org.Find(userId)?.Name ?? $"User {userId}");

    private static PersonRef? OptionalPerson(int? userId, OrgChart org) => userId is { } id ? Person(id, org) : null;
}
