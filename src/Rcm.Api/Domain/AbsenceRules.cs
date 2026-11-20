using System.Globalization;

namespace Rcm.Api.Domain;

public sealed record AbsenceChange(int UserId, DateOnly StartDate, DateOnly EndDate, int? CoverUserId, string? Note);

public enum AbsenceToday
{
    Upcoming,
    AwayCovered,
    AwayNeedsCover,
    Ended,
}

public static class AbsenceRules
{
    public const int MaxNoteLength = 500;

    // Supervisors record absences for themselves and their direct reports. Managers for anyone.
    public static bool MayManage(ActorContext actor, int personId, OrgChart org) => actor.Role switch
    {
        Role.Manager => true,
        Role.Supervisor => personId == actor.UserId || org.ManagerOf(personId) == actor.UserId,
        _ => false,
    };

    // A supervisor also sees, read-only, the absences they cover.
    public static bool CanView(ActorContext actor, AbsenceSnapshot absence, OrgChart org) =>
        MayManage(actor, absence.UserId, org) || (actor.Role != Role.Biller && absence.CoverUserId == actor.UserId);

    public static bool CanEdit(ActorContext actor, AbsenceSnapshot absence, OrgChart org, DateOnly today) =>
        MayManage(actor, absence.UserId, org) && absence.EndDate >= today;

    public static bool CanDelete(ActorContext actor, AbsenceSnapshot absence, OrgChart org, DateOnly today) =>
        MayManage(actor, absence.UserId, org) && absence.StartDate > today;

    // Checks a new absence (existing empty) or a change to one. Returns the change with the note trimmed.
    public static Result<AbsenceChange> Validate(
        AbsenceChange change, AbsenceSnapshot? existing, DateOnly today, OrgChart org, IReadOnlyCollection<AbsenceSnapshot> absences)
    {
        if (DateRule(change, existing, today) is { } dateError)
        {
            return dateError;
        }

        var person = org.Find(change.UserId);
        if (person is null)
        {
            return Error.Invalid("Unknown person.");
        }

        if (!person.IsActive)
        {
            return Error.Invalid($"{person.Name} is inactive.");
        }

        var others = absences.Where(a => a.Id != existing?.Id).ToList();
        var clash = others.FirstOrDefault(a => a.UserId == person.Id && a.Overlaps(change.StartDate, change.EndDate));
        if (clash is not null)
        {
            return Error.Invalid($"{person.Name} is already away from {Day(clash.StartDate)} to {Day(clash.EndDate)}.");
        }

        if (change.CoverUserId is { } coverId && CoverRule(person, coverId, change, today, org, others) is { } coverError)
        {
            return coverError;
        }

        var note = string.IsNullOrWhiteSpace(change.Note) ? null : change.Note.Trim();
        if (note?.Length > MaxNoteLength)
        {
            return Error.Invalid($"The note can be at most {MaxNoteLength} characters.");
        }

        return change with { Note = note };
    }

    public static AbsenceToday TodayState(
        AbsenceSnapshot absence, DateOnly today, IReadOnlyCollection<AbsenceSnapshot> absences, OrgChart org)
    {
        if (absence.EndDate < today)
        {
            return AbsenceToday.Ended;
        }

        if (absence.StartDate > today)
        {
            return AbsenceToday.Upcoming;
        }

        return CoverRules.StateOf(absence.UserId, today, absences, org).Status == CoverStatus.Covered
            ? AbsenceToday.AwayCovered
            : AbsenceToday.AwayNeedsCover;
    }

    // New absences start today or later. Once one has started its start stays and its end cannot move into the past.
    private static Error? DateRule(AbsenceChange change, AbsenceSnapshot? existing, DateOnly today)
    {
        if (existing is not null && existing.EndDate < today)
        {
            return Error.Invalid("This absence has ended and can no longer change.");
        }

        var started = existing is not null && existing.StartDate <= today;
        if (started && change.StartDate != existing!.StartDate)
        {
            return Error.Invalid("The absence has started, so its start date cannot change.");
        }

        if (!started && change.StartDate < today)
        {
            return Error.Invalid("An absence cannot start before today.");
        }

        if (change.EndDate < today)
        {
            return Error.Invalid("The end date cannot be before today.");
        }

        return change.EndDate < change.StartDate ? Error.Invalid("The end date is before the start date.") : null;
    }

    private static Error? CoverRule(
        OrgPerson person, int coverId, AbsenceChange change, DateOnly today, OrgChart org, IReadOnlyList<AbsenceSnapshot> others)
    {
        if (coverId == person.Id)
        {
            return Error.Invalid("Someone cannot cover their own absence.");
        }

        var cover = org.Find(coverId);
        if (cover is null)
        {
            return Error.Invalid("Unknown cover.");
        }

        if (!cover.IsActive)
        {
            return Error.Invalid($"{cover.Name} is inactive.");
        }

        // A biller can never cover a supervisor: that would let a biller approve a peer's work.
        if (cover.Role < person.Role)
        {
            return Error.Invalid($"{cover.Name} cannot cover {person.Name}. A cover must be a {RoleName(person.Role)} or higher.");
        }

        // Days already past do not count: a cover only grants rights from today on.
        var from = change.StartDate > today ? change.StartDate : today;
        var firstDayAway = others
            .Where(a => a.UserId == coverId && a.Overlaps(from, change.EndDate))
            .Select(a => a.StartDate > from ? a.StartDate : from)
            .Order()
            .Cast<DateOnly?>()
            .FirstOrDefault();
        return firstDayAway is { } day ? Error.Invalid($"{cover.Name} is away on {Day(day)}.") : null;
    }

    private static string Day(DateOnly date) => date.ToString("MMM d", CultureInfo.InvariantCulture);

    private static string RoleName(Role role) => role.ToString().ToLowerInvariant();
}
