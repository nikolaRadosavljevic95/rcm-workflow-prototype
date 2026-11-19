namespace Rcm.Api.Features.Users;

public static class UsersEndpoints
{
    public static IEndpointRouteBuilder MapUsersEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/users", (GetUsersHandler handler, CancellationToken ct) => handler.HandleAsync(ct));
        return app;
    }
}
