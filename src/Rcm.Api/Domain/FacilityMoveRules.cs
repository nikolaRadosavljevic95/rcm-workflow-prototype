namespace Rcm.Api.Domain;

public sealed record FacilityMove(int NewOwnerId, string Reason);

// What a move changed. MovedItemEvent is empty when no item went with the facility.
public sealed record FacilityMoveResult(FacilityAssignment Started, WorkItemEvent? MovedItemEvent);

// Permanent move of a facility to a new owner (docs/02).
public static class FacilityMoveRules
{
    public const int MaxReasonLength = 500;

    public static bool MayMove(Role role) => role == Role.Manager;

    // Rows of a closed month would move the open month's item, not the one on the row.
    public static bool ShowOnRows(Role role, CycleStatus monthStatus) => MayMove(role) && monthStatus == CycleStatus.Open;

    // Managers review and never own facilities.
    public static bool CanOwn(OrgPerson person) =>
        person.IsActive && person.Role is Role.Biller or Role.Supervisor;

    public static IReadOnlyList<OrgPerson> EligibleOwners(IEnumerable<OrgPerson> people) =>
        people.Where(CanOwn).OrderBy(p => p.Name, StringComparer.Ordinal).ToList();

    // Order of checks: caller, facility, new owner, reason.
    public static Result<FacilityMove> CanMove(
        Role role, bool facilityActive, int currentOwnerId, OrgPerson? newOwner, string? reason)
    {
        if (!MayMove(role))
        {
            return Error.Forbidden("Only a manager can move a facility.");
        }

        if (!facilityActive)
        {
            return Error.Invalid("The facility is inactive and cannot move.");
        }

        if (newOwner is null || !CanOwn(newOwner))
        {
            return Error.Invalid("The new owner must be an active biller or supervisor.");
        }

        if (newOwner.Id == currentOwnerId)
        {
            return Error.Invalid($"{newOwner.Name} already owns this facility.");
        }

        var trimmed = reason?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return Error.Invalid("A move needs a reason.");
        }

        if (trimmed.Length > MaxReasonLength)
        {
            return Error.Invalid($"A reason can be at most {MaxReasonLength} characters.");
        }

        return new FacilityMove(newOwner.Id, trimmed);
    }

    public static bool ItemMoves(WorkItemStatus status) => !status.IsFinal();

    // Pass the open month's item only when the manager asked to move it. A final item stays where it is.
    public static FacilityMoveResult Apply(
        Facility facility,
        FacilityAssignment? current,
        FacilityMove move,
        WorkItem? openItem,
        int actorId,
        DateTime utcNow)
    {
        current?.EndedAt = utcNow;
        var started = new FacilityAssignment
        {
            FacilityId = facility.Id,
            UserId = move.NewOwnerId,
            StartedAt = utcNow,
            Reason = move.Reason,
            ChangedById = actorId,
        };
        facility.OwnerId = move.NewOwnerId;

        if (openItem is null || !ItemMoves(openItem.Status))
        {
            return new FacilityMoveResult(started, null);
        }

        // Status, submitter and summary stay. The reviewer follows the new owner because it is never stored.
        var moved = new WorkItemEvent
        {
            Type = WorkItemEventType.Reassigned,
            FromUserId = openItem.AssigneeId,
            ToUserId = move.NewOwnerId,
            ActorId = actorId,
            Text = move.Reason,
            CreatedAt = utcNow,
        };
        openItem.AssigneeId = move.NewOwnerId;
        openItem.Version++;
        openItem.Events.Add(moved);
        return new FacilityMoveResult(started, moved);
    }
}
