using Microsoft.AspNetCore.Http.HttpResults;
using Rcm.Api.Auth;

namespace Rcm.Api.Features.Me;

public static class MeEndpoints
{
    public static IEndpointRouteBuilder MapMeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/me", async Task<Results<Ok<MeResponse>, NotFound>> (
            CurrentUser user, GetMeHandler handler, CancellationToken ct) =>
            await handler.HandleAsync(user, ct) is { } me ? TypedResults.Ok(me) : TypedResults.NotFound());
        return app;
    }
}
