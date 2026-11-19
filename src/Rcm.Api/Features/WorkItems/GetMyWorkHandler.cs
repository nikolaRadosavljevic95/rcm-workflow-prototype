using Rcm.Api.Auth;
using Rcm.Api.Domain;
using Rcm.Api.Features.Cycles;

namespace Rcm.Api.Features.WorkItems;

public sealed class GetMyWorkHandler(WorkItemReader reader)
{
    public async Task<Result<MyWorkResponse>> HandleAsync(CurrentUser user, int? cycleId, CancellationToken ct)
    {
        var cycle = await reader.FindCycleAsync(cycleId, ct);
        if (cycle is null)
        {
            return Error.NotFound("Month not found.");
        }

        var org = await reader.LoadOrgAsync(ct);
        var actor = WorkItemReader.ActorFor(user);
        var rows = (await reader.LoadItemsAsync(cycle.Id, ct))
            .Where(item => VisibilityRules.InMyWork(actor, item.AssigneeId))
            .Select(item => reader.ToRow(item, actor, org))
            .ToList();

        return new MyWorkResponse(
            CycleResponse.From(cycle),
            WorkItemReader.ByArOver90(rows.Where(r => WorkLists.NeedsAction(r.Status))),
            WorkItemReader.ByArOver90(rows.Where(r => !WorkLists.NeedsAction(r.Status))));
    }
}
