using Rcm.Api.Auth;

namespace Rcm.Api.Features.Cycles;

public static class CyclesEndpoints
{
    public static IEndpointRouteBuilder MapCyclesEndpoints(this IEndpointRouteBuilder app)
    {
        var cycles = app.MapGroup("/api/cycles");

        cycles.MapGet("/", (GetCyclesHandler handler, CancellationToken ct) => handler.HandleAsync(ct));

        cycles.MapPost("/", async (
            OpenCycleRequest request, CurrentUser user, OpenCycleHandler handler, CancellationToken ct) =>
            (await handler.HandleAsync(user, request, ct)).ToHttp());

        cycles.MapGet("/{id:int}/close-preview", async (
            int id, CurrentUser user, CloseCycleHandler handler, CancellationToken ct) =>
            (await handler.PreviewAsync(user, id, ct)).ToHttp());

        cycles.MapPost("/{id:int}/close", async (
            int id, CurrentUser user, CloseCycleHandler handler, CancellationToken ct) =>
            (await handler.CloseAsync(user, id, ct)).ToHttp());

        return app;
    }
}
