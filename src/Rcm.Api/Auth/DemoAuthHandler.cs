using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Rcm.Api.Data;

namespace Rcm.Api.Auth;

// Demo only: the caller says who they are. Registered in Development, never in production.
public sealed class DemoAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    AppDbContext db)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Demo";
    public const string HeaderName = "X-Demo-User";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var header))
        {
            return AuthenticateResult.NoResult();
        }

        if (!int.TryParse(header.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out var userId))
        {
            return AuthenticateResult.Fail($"{HeaderName} must be a user id.");
        }

        var user = await db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == userId && u.IsActive, Context.RequestAborted);
        if (user is null)
        {
            return AuthenticateResult.Fail("Unknown or inactive user.");
        }

        Claim[] claims =
        [
            new(CurrentUser.UserIdClaim, user.Id.ToString(CultureInfo.InvariantCulture)),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Role, user.Role.ToString()),
        ];
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));
    }
}
