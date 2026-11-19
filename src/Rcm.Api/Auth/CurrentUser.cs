using System.Globalization;
using System.Security.Claims;
using Rcm.Api.Domain;

namespace Rcm.Api.Auth;

// The signed-in user, read from the claims any authentication scheme issues.
public sealed record CurrentUser(int Id, string Name, Role Role)
{
    public const string UserIdClaim = "user_id";

    public static CurrentUser? FromPrincipal(ClaimsPrincipal principal)
    {
        var id = principal.FindFirstValue(UserIdClaim);
        var name = principal.FindFirstValue(ClaimTypes.Name);
        var role = principal.FindFirstValue(ClaimTypes.Role);

        return int.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var userId)
            && name is not null
            && Enum.TryParse<Role>(role, out var parsedRole)
                ? new CurrentUser(userId, name, parsedRole)
                : null;
    }

    // Lets endpoints take CurrentUser as a parameter. A missing user fails binding before the handler runs.
    public static ValueTask<CurrentUser?> BindAsync(HttpContext context) =>
        ValueTask.FromResult(FromPrincipal(context.User));
}
