using Rcm.Api.Auth;
using Rcm.Api.Domain;
using Rcm.Api.Features.Cycles;

namespace Rcm.Api.Features.WorkItems;

public sealed class GetMyWorkHandler(WorkItemReader reader, RequestContextLoader contexts)
{
    public async Task<Result<MyWorkResponse>> HandleAsync(CurrentUser user, int? cycleId, CancellationToken ct)
    {
        var cycle = await reader.FindCycleAsync(cycleId, ct);
        if (cycle is null)
        {
            return Error.NotFound("Month not found.");
        }

        var context = await contexts.LoadAsync(user, ct);
        var rows = (await reader.LoadItemsAsync(cycle.Id, ct))
            .Where(item => VisibilityRules.InMyWork(context.Actor, item.AssigneeId))
            .Select(item => WorkItemReader.ToRow(item, context))
            .ToList();

        return new MyWorkResponse(
            CycleResponse.From(cycle),
            WorkItemReader.ByArOver90(rows.Where(r => WorkLists.NeedsAction(r.Status))),
            WorkItemReader.ByArOver90(rows.Where(r => !WorkLists.NeedsAction(r.Status))));
    }
}
