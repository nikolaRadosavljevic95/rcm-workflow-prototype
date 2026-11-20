using Rcm.Api.Auth;

namespace Rcm.Api.Features.Facilities;

public static class FacilitiesEndpoints
{
    public static IEndpointRouteBuilder MapFacilitiesEndpoints(this IEndpointRouteBuilder app)
    {
        var facilities = app.MapGroup("/api/facilities");

        facilities.MapGet("/", async (CurrentUser user, GetFacilitiesHandler handler, CancellationToken ct) =>
            (await handler.HandleAsync(user, ct)).ToHttp());

        facilities.MapPut("/{id:int}/owner", async (
            int id, MoveFacilityRequest request, CurrentUser user, MoveFacilityHandler handler, CancellationToken ct) =>
            (await handler.HandleAsync(user, id, request, ct)).ToHttp());

        return app;
    }
}
