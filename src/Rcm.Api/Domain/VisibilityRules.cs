namespace Rcm.Api.Domain;

public static class VisibilityRules
{
    public static bool HasOverview(Role role) => role is Role.Supervisor or Role.Manager;

    // Managers see every item. A supervisor sees their own, their direct reports', and the same for anyone
    // they cover. A biller sees their own and those of anyone they cover.
    public static bool CanSee(ActorContext actor, int ownerId, OrgChart org)
    {
        if (actor.Role == Role.Manager || actor.ActsFor(ownerId))
        {
            return true;
        }

        return actor.Role == Role.Supervisor && org.ManagerOf(ownerId) is { } managerId && actor.ActsFor(managerId);
    }

    public static bool InMyWork(ActorContext actor, int ownerId) => actor.ActsFor(ownerId);

    public static bool AwaitingMyReview(ActorContext actor, ItemSnapshot item, OrgChart org) =>
        item.Status == WorkItemStatus.Submitted
        && actor.UserId != item.SubmittedById
        && ReviewerRules.IsReviewer(actor, item.OwnerId, org);
}
