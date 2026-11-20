namespace Rcm.Api.Domain;

// Anyone who can see an item may comment while its month is open. Visibility is checked by the caller.
public static class CommentRules
{
    public static Result<string> Check(bool monthOpen, string? text)
    {
        if (!monthOpen)
        {
            return Error.Invalid("The month is closed. Its items are read-only.");
        }

        var trimmed = text?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return Error.Invalid("A comment needs text.");
        }

        if (trimmed.Length > WorkflowRules.MaxTextLength)
        {
            return Error.Invalid($"A comment can be at most {WorkflowRules.MaxTextLength} characters.");
        }

        return trimmed;
    }

    // A comment is for someone else only when the commenter sees the item through a cover:
    // they cover the owner, or they cover the supervisor the owner reports to.
    public static int? OnBehalfOf(ActorContext actor, int ownerId, OrgChart org)
    {
        var supervisorId = org.ManagerOf(ownerId);
        if (actor.Role == Role.Manager || actor.UserId == ownerId || supervisorId == actor.UserId)
        {
            return null;
        }

        if (actor.CoversToday.Contains(ownerId))
        {
            return ownerId;
        }

        return supervisorId is { } id && actor.CoversToday.Contains(id) ? id : null;
    }
}
