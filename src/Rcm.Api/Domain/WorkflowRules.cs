namespace Rcm.Api.Domain;

public sealed record Decision(WorkflowAction Action, WorkItemStatus To, string? Text, int? OnBehalfOfId);

// The transition table and its permissions from docs/02.
public static class WorkflowRules
{
    public const int MaxBlockedReasonLength = 500;
    public const int MaxTextLength = 2000;

    private static readonly Dictionary<(WorkItemStatus From, WorkflowAction Action), WorkItemStatus> Transitions = new()
    {
        [(WorkItemStatus.NotStarted, WorkflowAction.Start)] = WorkItemStatus.InProgress,
        [(WorkItemStatus.NotStarted, WorkflowAction.Block)] = WorkItemStatus.Blocked,
        [(WorkItemStatus.InProgress, WorkflowAction.Block)] = WorkItemStatus.Blocked,
        [(WorkItemStatus.Returned, WorkflowAction.Block)] = WorkItemStatus.Blocked,
        [(WorkItemStatus.Blocked, WorkflowAction.Unblock)] = WorkItemStatus.InProgress,
        [(WorkItemStatus.NotStarted, WorkflowAction.Submit)] = WorkItemStatus.Submitted,
        [(WorkItemStatus.InProgress, WorkflowAction.Submit)] = WorkItemStatus.Submitted,
        [(WorkItemStatus.Returned, WorkflowAction.Submit)] = WorkItemStatus.Submitted,
        [(WorkItemStatus.Submitted, WorkflowAction.Approve)] = WorkItemStatus.Approved,
        [(WorkItemStatus.Submitted, WorkflowAction.Return)] = WorkItemStatus.Returned,
    };

    public static IReadOnlyList<WorkflowAction> AllowedActions(ActorContext actor, ItemSnapshot item, OrgChart org) =>
        Enum.GetValues<WorkflowAction>().Where(action => Check(actor, item, org, action).IsSuccess).ToList();

    // Order of checks: closed month, action not valid from this status, actor not allowed, missing text.
    public static Result<Decision> Decide(
        ActorContext actor, ItemSnapshot item, OrgChart org, WorkflowAction action, string? text)
    {
        var check = Check(actor, item, org, action);
        if (!check.IsSuccess)
        {
            return check.Error;
        }

        string? keptText = null;
        if (TextRuleFor(action) is { } rule)
        {
            var trimmed = text?.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                return Error.Invalid($"{action} needs a {rule.Noun}.");
            }

            if (trimmed.Length > rule.MaxLength)
            {
                return Error.Invalid($"The {rule.Noun} can be at most {rule.MaxLength} characters.");
            }

            keptText = trimmed;
        }

        var (to, actingAs) = check.Value;
        return new Decision(action, to, keptText, actingAs.OnBehalfOfId);
    }

    // Writes the decision onto the item and appends its event. The caller saves both together.
    public static WorkItemEvent Apply(WorkItem item, Decision decision, int actorId, DateTime utcNow)
    {
        var from = item.Status;
        switch (decision.Action)
        {
            case WorkflowAction.Block:
                item.BlockedReason = decision.Text;
                break;
            case WorkflowAction.Unblock:
                item.BlockedReason = null;
                break;
            case WorkflowAction.Submit:
                item.Summary = decision.Text;
                item.SubmittedById = actorId;
                item.SubmittedAt = utcNow;
                break;
            case WorkflowAction.Approve:
                item.ApprovedById = actorId;
                item.ApprovedAt = utcNow;
                break;
        }

        item.Status = decision.To;
        item.Version++;

        var change = new WorkItemEvent
        {
            Type = WorkItemEventType.StatusChanged,
            FromStatus = from,
            ToStatus = decision.To,
            ActorId = actorId,
            OnBehalfOfId = decision.OnBehalfOfId,
            Text = decision.Text,
            CreatedAt = utcNow,
        };
        item.Events.Add(change);
        return change;
    }

    private static Result<(WorkItemStatus To, ActingAs ActingAs)> Check(
        ActorContext actor, ItemSnapshot item, OrgChart org, WorkflowAction action)
    {
        if (!item.MonthOpen)
        {
            return Error.Invalid("The month is closed. Its items are read-only.");
        }

        if (!Transitions.TryGetValue((item.Status, action), out var to))
        {
            return Error.Invalid($"{action} is not possible while the item is {item.Status}.");
        }

        var actingAs = action is WorkflowAction.Approve or WorkflowAction.Return
            ? ReviewerRules.MayReview(actor, item, org)
            : MayWorkOn(actor, item);
        if (!actingAs.IsSuccess)
        {
            return actingAs.Error;
        }

        return (to, actingAs.Value);
    }

    // Owner actions: the owner, or whoever covers the owner today.
    private static Result<ActingAs> MayWorkOn(ActorContext actor, ItemSnapshot item)
    {
        if (actor.UserId == item.OwnerId)
        {
            return new ActingAs(null);
        }

        return actor.CoversToday.Contains(item.OwnerId)
            ? new ActingAs(item.OwnerId)
            : Error.Forbidden("Only the owner or the owner's cover can work on this item.");
    }

    private sealed record TextRule(string Noun, int MaxLength);

    private static TextRule? TextRuleFor(WorkflowAction action) => action switch
    {
        WorkflowAction.Block => new TextRule("reason", MaxBlockedReasonLength),
        WorkflowAction.Submit => new TextRule("summary", MaxTextLength),
        WorkflowAction.Return => new TextRule("comment", MaxTextLength),
        _ => null,
    };
}
