namespace Rcm.Api.Domain;

public static class ReviewerRules
{
    // The person the owner reports to. Empty means any manager other than the owner.
    public static int? ReviewerOf(int ownerId, OrgChart org) => org.ManagerOf(ownerId);

    // The reviewer or the reviewer's cover. A manager's right to review any item does not count here.
    public static bool IsReviewer(ActorContext actor, int ownerId, OrgChart org) =>
        ReviewerOf(ownerId, org) is { } reviewerId
            ? actor.ActsFor(reviewerId)
            : actor.Role == Role.Manager && actor.UserId != ownerId;

    // Approve and Return: the reviewer, the reviewer's cover or any manager. Never the owner or the submitter.
    public static Result<ActingAs> MayReview(ActorContext actor, ItemSnapshot item, OrgChart org)
    {
        if (actor.UserId == item.SubmittedById || actor.UserId == item.OwnerId)
        {
            return Error.Forbidden("Nobody reviews their own work. Someone else has to approve or return this item.");
        }

        var reviewerId = ReviewerOf(item.OwnerId, org);
        if (reviewerId == actor.UserId)
        {
            return new ActingAs(null);
        }

        if (reviewerId is { } absentReviewerId && actor.CoversToday.Contains(absentReviewerId))
        {
            return new ActingAs(absentReviewerId);
        }

        return actor.Role == Role.Manager
            ? new ActingAs(null)
            : Error.Forbidden("Only the reviewer, the reviewer's cover or a manager can review this item.");
    }
}
