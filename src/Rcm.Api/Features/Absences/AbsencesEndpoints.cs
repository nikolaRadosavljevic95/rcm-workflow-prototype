using Rcm.Api.Auth;

namespace Rcm.Api.Features.Absences;

public static class AbsencesEndpoints
{
    public static IEndpointRouteBuilder MapAbsencesEndpoints(this IEndpointRouteBuilder app)
    {
        var absences = app.MapGroup("/api/absences");

        absences.MapGet("/", async (CurrentUser user, GetAbsencesHandler handler, CancellationToken ct) =>
            (await handler.HandleAsync(user, ct)).ToHttp());

        absences.MapPost("/", async (
            CreateAbsenceRequest request, CurrentUser user, CreateAbsenceHandler handler, CancellationToken ct) =>
            (await handler.HandleAsync(user, request, ct)).ToHttp());

        absences.MapPut("/{id:int}", async (
            int id, UpdateAbsenceRequest request, CurrentUser user, UpdateAbsenceHandler handler, CancellationToken ct) =>
            (await handler.HandleAsync(user, id, request, ct)).ToHttp());

        absences.MapDelete("/{id:int}", async (int id, CurrentUser user, DeleteAbsenceHandler handler, CancellationToken ct) =>
            await handler.HandleAsync(user, id, ct) is { } error ? error.ToHttp() : TypedResults.NoContent());

        return app;
    }
}
