using Rcm.Api.Auth;
using Rcm.Api.Domain;

namespace Rcm.Api.Features.WorkItems;

public static class WorkItemsEndpoints
{
    public static IEndpointRouteBuilder MapWorkItemsEndpoints(this IEndpointRouteBuilder app)
    {
        var items = app.MapGroup("/api/work-items");

        items.MapGet("/", async (
            string? view, int? cycleId, CurrentUser user,
            GetMyWorkHandler myWork, GetOverviewHandler overview, CancellationToken ct) => view switch
        {
            "mine" => (await myWork.HandleAsync(user, cycleId, ct)).ToHttp(),
            "overview" => (await overview.HandleAsync(user, cycleId, ct)).ToHttp(),
            _ => Error.Invalid("view must be mine or overview.").ToHttp(),
        });

        items.MapGet("/{id:int}", async (int id, CurrentUser user, GetWorkItemHandler handler, CancellationToken ct) =>
            (await handler.HandleAsync(user, id, ct)).ToHttp());

        items.MapPost("/{id:int}/transitions", async (
            int id, TransitionRequest request, CurrentUser user, TransitionWorkItemHandler handler, CancellationToken ct) =>
            (await handler.HandleAsync(user, id, request, ct)).ToHttp());

        items.MapPost("/{id:int}/comments", async (
            int id, CommentRequest request, CurrentUser user, AddCommentHandler handler, CancellationToken ct) =>
            (await handler.HandleAsync(user, id, request, ct)).ToHttp());

        return app;
    }
}
