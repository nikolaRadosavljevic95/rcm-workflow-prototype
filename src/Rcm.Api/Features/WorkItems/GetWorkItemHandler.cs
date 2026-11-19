using Rcm.Api.Auth;
using Rcm.Api.Domain;

namespace Rcm.Api.Features.WorkItems;

public sealed class GetWorkItemHandler(WorkItemReader reader)
{
    public async Task<Result<WorkItemDetail>> HandleAsync(CurrentUser user, int itemId, CancellationToken ct)
    {
        var org = await reader.LoadOrgAsync(ct);
        var detail = await reader.LoadDetailAsync(itemId, WorkItemReader.ActorFor(user), org, ct);
        return detail is null ? Error.NotFound("Work item not found.") : detail;
    }
}
