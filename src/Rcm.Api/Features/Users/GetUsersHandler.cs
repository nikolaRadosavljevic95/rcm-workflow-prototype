using Microsoft.EntityFrameworkCore;
using Rcm.Api.Data;
using Rcm.Api.Domain;

namespace Rcm.Api.Features.Users;

public sealed record UserResponse(int Id, string Name, Role Role, int? ReportsToId);

public sealed class GetUsersHandler(AppDbContext db)
{
    private static readonly Role[] RolesTopDown = [Role.Manager, Role.Supervisor, Role.Biller];

    public async Task<IReadOnlyList<UserResponse>> HandleAsync(CancellationToken ct)
    {
        var users = await db.Users
            .AsNoTracking()
            .Where(u => u.IsActive)
            .Select(u => new UserResponse(u.Id, u.Name, u.Role, u.ReportsToId))
            .ToListAsync(ct);

        return InTeamOrder(users);
    }

    // Role by role from the top, each team kept together in the order of the people it reports to:
    // Morgan's supervisors before Quinn's, Sam's billers before Taylor's. Inside a team, in the order users were added.
    // Someone whose manager is not in the list goes last in their role.
    private static List<UserResponse> InTeamOrder(IReadOnlyList<UserResponse> users)
    {
        var ordered = new List<UserResponse>(users.Count);
        foreach (var role in RolesTopDown)
        {
            var position = ordered.Select((u, index) => (u.Id, index)).ToDictionary(p => p.Id, p => p.index);
            ordered.AddRange(users
                .Where(u => u.Role == role)
                .OrderBy(u => u.ReportsToId is { } id && position.TryGetValue(id, out var index) ? index : int.MaxValue)
                .ThenBy(u => u.Id));
        }

        return ordered;
    }
}
