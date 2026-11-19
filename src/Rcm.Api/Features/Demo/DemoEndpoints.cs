using Rcm.Api.Features.Users;

namespace Rcm.Api.Features.Demo;

// Development only. The sign-in picker needs the user list before anyone is signed in.
public static class DemoEndpoints
{
    public static IEndpointRouteBuilder MapDemoEndpoints(this IEndpointRouteBuilder app)
    {
        var demo = app.MapGroup("/api/demo").AllowAnonymous();

        demo.MapGet("/users", (GetUsersHandler handler, CancellationToken ct) => handler.HandleAsync(ct));

        demo.MapPost("/reset", async (ResetDemoDataHandler handler, CancellationToken ct) =>
        {
            await handler.HandleAsync(ct);
            return TypedResults.NoContent();
        });

        return app;
    }
}
