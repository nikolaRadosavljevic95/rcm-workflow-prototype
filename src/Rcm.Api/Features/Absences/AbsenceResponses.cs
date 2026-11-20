using Rcm.Api.Domain;

namespace Rcm.Api.Features.Absences;

public sealed record AbsenceRow(
    int Id,
    PersonRef Person,
    DateOnly StartDate,
    DateOnly EndDate,
    PersonRef? Cover,
    string? Note,
    AbsenceToday Today,
    bool CanEdit,
    bool CanDelete)
{
    public static AbsenceRow From(Absence absence, RequestContext context)
    {
        var snapshot = Snapshot(absence);
        return new AbsenceRow(
            absence.Id,
            context.Person(absence.UserId),
            absence.StartDate,
            absence.EndDate,
            context.OptionalPerson(absence.CoverUserId),
            absence.Note,
            AbsenceRules.TodayState(snapshot, context.Today, context.AbsencesToday, context.Org),
            AbsenceRules.CanEdit(context.Actor, snapshot, context.Org, context.Today),
            AbsenceRules.CanDelete(context.Actor, snapshot, context.Org, context.Today));
    }

    public static AbsenceSnapshot Snapshot(Absence absence) =>
        new(absence.Id, absence.UserId, absence.StartDate, absence.EndDate, absence.CoverUserId);
}

// People lists who the viewer may record an absence for.
public sealed record AbsencesResponse(IReadOnlyList<PersonRef> People, IReadOnlyList<AbsenceRow> Absences);

public sealed record CreateAbsenceRequest(int UserId, DateOnly StartDate, DateOnly EndDate, int? CoverUserId, string? Note);

public sealed record UpdateAbsenceRequest(DateOnly StartDate, DateOnly EndDate, int? CoverUserId, string? Note);
