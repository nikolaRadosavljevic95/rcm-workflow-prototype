namespace Rcm.Api.Domain;

// Both dates are inclusive.
public sealed record AbsenceSnapshot(int Id, int UserId, DateOnly StartDate, DateOnly EndDate, int? CoverUserId)
{
    public bool Covers(DateOnly date) => StartDate <= date && date <= EndDate;

    public bool Overlaps(DateOnly start, DateOnly end) => StartDate <= end && start <= EndDate;
}

public enum CoverStatus
{
    Present,
    Covered,
    NeedsCover,
}

public sealed record CoverState(CoverStatus Status, int? CoverId)
{
    public static readonly CoverState Present = new(CoverStatus.Present, null);
    public static readonly CoverState NeedsCover = new(CoverStatus.NeedsCover, null);

    public static CoverState CoveredBy(int coverId) => new(CoverStatus.Covered, coverId);
}

// A cover is a dated permission, not a reassignment (docs/02, Covers).
public static class CoverRules
{
    public static AbsenceSnapshot? AbsenceOn(int userId, DateOnly date, IEnumerable<AbsenceSnapshot> absences) =>
        absences.FirstOrDefault(a => a.UserId == userId && a.Covers(date));

    public static bool IsAway(int userId, DateOnly date, IEnumerable<AbsenceSnapshot> absences) =>
        AbsenceOn(userId, date, absences) is not null;

    // A cover counts only when set, active and not away that day. No chains: a cover who is away leaves nobody.
    public static CoverState StateOf(int ownerId, DateOnly date, IReadOnlyCollection<AbsenceSnapshot> absences, OrgChart org)
    {
        var absence = AbsenceOn(ownerId, date, absences);
        if (absence is null)
        {
            return CoverState.Present;
        }

        return absence.CoverUserId is { } coverId
            && org.Find(coverId) is { IsActive: true }
            && !IsAway(coverId, date, absences)
                ? CoverState.CoveredBy(coverId)
                : CoverState.NeedsCover;
    }

    public static IReadOnlySet<int> CoversToday(
        int userId, DateOnly date, IReadOnlyCollection<AbsenceSnapshot> absences, OrgChart org) =>
        absences
            .Where(a => a.CoverUserId == userId && a.Covers(date))
            .Select(a => a.UserId)
            .Where(ownerId => StateOf(ownerId, date, absences, org).CoverId == userId)
            .ToHashSet();
}
