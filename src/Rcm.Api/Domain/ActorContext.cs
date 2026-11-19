namespace Rcm.Api.Domain;

// Who is acting, and whose work they may do today because they cover for them.
public sealed record ActorContext(int UserId, Role Role, IReadOnlySet<int> CoversToday)
{
    public bool ActsFor(int userId) => userId == UserId || CoversToday.Contains(userId);
}

// Set when the actor works for someone they cover; empty when they act for themselves.
public sealed record ActingAs(int? OnBehalfOfId);
