namespace Rcm.Api.Features.Cycles;

public static class CyclesEndpoints
{
    public static IEndpointRouteBuilder MapCyclesEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/cycles", (GetCyclesHandler handler, CancellationToken ct) => handler.HandleAsync(ct));
        return app;
    }
}
