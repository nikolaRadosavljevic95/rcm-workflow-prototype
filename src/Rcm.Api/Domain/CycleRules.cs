using System.Globalization;

namespace Rcm.Api.Domain;

public sealed record CycleSnapshot(int Year, int Month, CycleStatus Status)
{
    public YearMonth Period => new(Year, Month);
}

// What a manager may do with months while looking at one of them. Empty for everyone else.
public sealed record MonthActionsDecision(bool CanClose, YearMonth? OpenNext);

// Opening and closing a month (docs/02). One month is open at a time, and only a manager opens or closes one.
public static class CycleRules
{
    public const int DefaultDueDay = 15;

    // The month after the latest one. With no months at all, the current month.
    public static YearMonth NextToOpen(IEnumerable<CycleSnapshot> cycles, DateOnly today) =>
        cycles.MaxBy(c => (c.Year * 12) + c.Month)?.Period.Next() ?? YearMonth.Of(today);

    public static DateOnly DefaultDueDate(YearMonth month) => month.Day(DefaultDueDay);

    public static string Label(YearMonth month) =>
        $"{CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(month.Month)} {month.Year}";

    public static Result<YearMonth> CanOpen(
        Role role, IReadOnlyCollection<CycleSnapshot> cycles, DateOnly today, DateOnly? dueDate)
    {
        if (role != Role.Manager)
        {
            return Error.Forbidden("Only a manager can open a month.");
        }

        if (cycles.Any(c => c.Status == CycleStatus.Open))
        {
            return Error.Invalid("Close the open month before opening the next one.");
        }

        var next = NextToOpen(cycles, today);
        if (dueDate is null)
        {
            return Error.Invalid("A due date is required.");
        }

        if (!next.Contains(dueDate.Value))
        {
            return Error.Invalid($"The due date must fall inside {Label(next)}.");
        }

        return next;
    }

    // Also guards the close preview, which only makes sense for a month that can still close.
    public static Result<YearMonth> CanClose(Role role, CycleSnapshot cycle)
    {
        if (role != Role.Manager)
        {
            return Error.Forbidden("Only a manager can close a month.");
        }

        if (cycle.Status != CycleStatus.Open)
        {
            return Error.Invalid($"{Label(cycle.Period)} is already closed.");
        }

        return cycle.Period;
    }

    public static MonthActionsDecision ActionsFor(
        Role role, CycleSnapshot shown, IReadOnlyCollection<CycleSnapshot> cycles, DateOnly today) =>
        role != Role.Manager
            ? new MonthActionsDecision(false, null)
            : new MonthActionsDecision(
                CanClose(role, shown).IsSuccess,
                cycles.Any(c => c.Status == CycleStatus.Open) ? null : NextToOpen(cycles, today));

    // Submitted items close too: the review did not finish inside the month.
    public static bool ClosesAsIncomplete(WorkItemStatus status) =>
        status is not (WorkItemStatus.Approved or WorkItemStatus.Incomplete);

    public static WorkItem NewItem(
        ReviewCycle cycle, int facilityId, int ownerId, int? previousItemId, int actorId, DateTime utcNow) =>
        new()
        {
            Cycle = cycle,
            FacilityId = facilityId,
            AssigneeId = ownerId,
            Status = WorkItemStatus.NotStarted,
            DueDate = cycle.DueDate,
            PreviousItemId = previousItemId,
            Events =
            [
                new WorkItemEvent
                {
                    Type = WorkItemEventType.Created,
                    ToStatus = WorkItemStatus.NotStarted,
                    ToUserId = ownerId,
                    ActorId = actorId,
                    CreatedAt = utcNow,
                },
            ],
        };

    public static WorkItemEvent CloseItem(WorkItem item, int actorId, DateTime utcNow)
    {
        var change = new WorkItemEvent
        {
            Type = WorkItemEventType.StatusChanged,
            FromStatus = item.Status,
            ToStatus = WorkItemStatus.Incomplete,
            ActorId = actorId,
            CreatedAt = utcNow,
        };
        item.Status = WorkItemStatus.Incomplete;
        item.Version++;
        item.Events.Add(change);
        return change;
    }
}
