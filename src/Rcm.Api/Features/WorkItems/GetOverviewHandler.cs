using Rcm.Api.Auth;
using Rcm.Api.Domain;
using Rcm.Api.Features.Cycles;

namespace Rcm.Api.Features.WorkItems;

public sealed class GetOverviewHandler(WorkItemReader reader)
{
    public async Task<Result<OverviewResponse>> HandleAsync(CurrentUser user, int? cycleId, CancellationToken ct)
    {
        if (!VisibilityRules.HasOverview(user.Role))
        {
            return Error.Forbidden("Overview is for supervisors and managers.");
        }

        var cycle = await reader.FindCycleAsync(cycleId, ct);
        if (cycle is null)
        {
            return Error.NotFound("Month not found.");
        }

        var org = await reader.LoadOrgAsync(ct);
        var actor = WorkItemReader.ActorFor(user);
        var rows = (await reader.LoadItemsAsync(cycle.Id, ct))
            .Where(item => VisibilityRules.CanSee(actor, item.AssigneeId, org))
            .Select(item => reader.ToRow(item, actor, org))
            .ToList();

        var people = rows
            .GroupBy(r => r.Owner)
            .Select(g => new PersonSummary(
                g.Key,
                g.Count(r => WorkLists.NeedsAction(r.Status)),
                g.Count(r => r.Status == WorkItemStatus.Submitted),
                g.Count(r => r.Status == WorkItemStatus.Approved)))
            .OrderBy(p => p.Person.Name, StringComparer.Ordinal)
            .ToList();

        var isManager = user.Role == Role.Manager;
        return new OverviewResponse(
            CycleResponse.From(cycle),
            WorkItemReader.ByArOver90(rows.Where(r => r.AwaitingMyReview)),
            WorkItemReader.ByArOver90(rows.Where(r => WorkLists.NeedsAttention(r.Status, r.Flags))),
            people,
            WorkItemReader.ByArOver90(rows),
            isManager ? Progress(rows) : null,
            isManager ? Teams(rows) : null);
    }

    private static List<StatusCount> Progress(IReadOnlyList<WorkItemRow> rows) =>
        WorkLists.ProgressOrder
            .Select(status => new StatusCount(status, rows.Count(r => r.Status == status)))
            .Where(count => count.Count > 0)
            .ToList();

    private static List<TeamSummary> Teams(IReadOnlyList<WorkItemRow> rows) =>
        rows
            .GroupBy(r => r.Team)
            .Select(g => new TeamSummary(
                g.Key,
                g.Count(),
                g.Count(r => r.Status == WorkItemStatus.Approved),
                g.Count(r => r.Status == WorkItemStatus.Submitted),
                g.Count(r => WorkLists.NeedsAction(r.Status)),
                g.Count(r => r.Flags.Contains(ItemFlag.Overdue))))
            .OrderBy(t => t.Supervisor.Name, StringComparer.Ordinal)
            .ToList();
}
