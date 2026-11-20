using Rcm.Api.Auth;
using Rcm.Api.Domain;

namespace Rcm.Api.Features.WorkItems;

public sealed class GetWorkItemHandler(WorkItemReader reader, RequestContextLoader contexts)
{
    public async Task<Result<WorkItemDetail>> HandleAsync(CurrentUser user, int itemId, CancellationToken ct)
    {
        var context = await contexts.LoadAsync(user, ct);
        var detail = await reader.LoadDetailAsync(itemId, context, ct);
        return detail is null ? Error.NotFound("Work item not found.") : detail;
    }
}
