using Microsoft.EntityFrameworkCore;
using Rcm.Api.Auth;
using Rcm.Api.Data;
using Rcm.Api.Domain;

namespace Rcm.Api.Features.Absences;

public sealed class GetAbsencesHandler(AppDbContext db, RequestContextLoader contexts)
{
    // Absences that end today or later, in the viewer's scope.
    public async Task<Result<AbsencesResponse>> HandleAsync(CurrentUser user, CancellationToken ct)
    {
        if (!VisibilityRules.HasOverview(user.Role))
        {
            return Error.Forbidden("Absences are for supervisors and managers.");
        }

        var context = await contexts.LoadAsync(user, ct);
        var absences = await db.Absences.AsNoTracking().Where(a => a.EndDate >= context.Today).ToListAsync(ct);
        var people = await db.Users
            .AsNoTracking()
            .Where(u => u.IsActive)
            .Select(u => u.Id)
            .ToListAsync(ct);

        return new AbsencesResponse(
            people
                .Where(id => AbsenceRules.MayManage(context.Actor, id, context.Org))
                .Select(context.Person)
                .OrderBy(p => p.Name, StringComparer.Ordinal)
                .ToList(),
            absences
                .Where(a => AbsenceRules.CanView(context.Actor, AbsenceRow.Snapshot(a), context.Org))
                .Select(a => AbsenceRow.From(a, context))
                .OrderBy(a => a.StartDate)
                .ThenBy(a => a.Person.Name, StringComparer.Ordinal)
                .ToList());
    }
}

public sealed class CreateAbsenceHandler(AppDbContext db, RequestContextLoader contexts, BusinessCalendar calendar)
{
    public async Task<Result<AbsenceRow>> HandleAsync(CurrentUser user, CreateAbsenceRequest request, CancellationToken ct)
    {
        var context = await contexts.LoadAsync(user, ct);
        if (!AbsenceRules.MayManage(context.Actor, request.UserId, context.Org))
        {
            return Error.Forbidden("You can record absences only for yourself and your team.");
        }

        var change = new AbsenceChange(request.UserId, request.StartDate, request.EndDate, request.CoverUserId, request.Note);
        var all = await AbsenceStore.SnapshotsAsync(db, ct);
        var valid = AbsenceRules.Validate(change, null, context.Today, context.Org, all);
        if (!valid.IsSuccess)
        {
            return valid.Error;
        }

        var absence = new Absence
        {
            UserId = valid.Value.UserId,
            StartDate = valid.Value.StartDate,
            EndDate = valid.Value.EndDate,
            CoverUserId = valid.Value.CoverUserId,
            Note = valid.Value.Note,
            CreatedById = context.Actor.UserId,
            CreatedAt = calendar.UtcNow,
        };
        db.Absences.Add(absence);
        await db.SaveChangesAsync(ct);

        return AbsenceRow.From(absence, await contexts.LoadAsync(user, ct));
    }
}

public sealed class UpdateAbsenceHandler(AppDbContext db, RequestContextLoader contexts)
{
    public async Task<Result<AbsenceRow>> HandleAsync(
        CurrentUser user, int absenceId, UpdateAbsenceRequest request, CancellationToken ct)
    {
        var context = await contexts.LoadAsync(user, ct);
        var absence = await db.Absences.SingleOrDefaultAsync(a => a.Id == absenceId, ct);
        if (absence is null || !AbsenceRules.CanView(context.Actor, AbsenceRow.Snapshot(absence), context.Org))
        {
            return Error.NotFound("Absence not found.");
        }

        if (!AbsenceRules.MayManage(context.Actor, absence.UserId, context.Org))
        {
            return Error.Forbidden("You can change absences only for yourself and your team.");
        }

        var change = new AbsenceChange(absence.UserId, request.StartDate, request.EndDate, request.CoverUserId, request.Note);
        var all = await AbsenceStore.SnapshotsAsync(db, ct);
        var valid = AbsenceRules.Validate(change, AbsenceRow.Snapshot(absence), context.Today, context.Org, all);
        if (!valid.IsSuccess)
        {
            return valid.Error;
        }

        absence.StartDate = valid.Value.StartDate;
        absence.EndDate = valid.Value.EndDate;
        absence.CoverUserId = valid.Value.CoverUserId;
        absence.Note = valid.Value.Note;
        await db.SaveChangesAsync(ct);

        return AbsenceRow.From(absence, await contexts.LoadAsync(user, ct));
    }
}

public sealed class DeleteAbsenceHandler(AppDbContext db, RequestContextLoader contexts)
{
    // Empty on success.
    public async Task<Error?> HandleAsync(CurrentUser user, int absenceId, CancellationToken ct)
    {
        var context = await contexts.LoadAsync(user, ct);
        var absence = await db.Absences.SingleOrDefaultAsync(a => a.Id == absenceId, ct);
        if (absence is null || !AbsenceRules.CanView(context.Actor, AbsenceRow.Snapshot(absence), context.Org))
        {
            return Error.NotFound("Absence not found.");
        }

        if (!AbsenceRules.MayManage(context.Actor, absence.UserId, context.Org))
        {
            return Error.Forbidden("You can delete absences only for yourself and your team.");
        }

        if (!AbsenceRules.CanDelete(context.Actor, AbsenceRow.Snapshot(absence), context.Org, context.Today))
        {
            return Error.Invalid("An absence can be deleted only before it starts. Shorten it instead.");
        }

        db.Absences.Remove(absence);
        await db.SaveChangesAsync(ct);
        return null;
    }
}

internal static class AbsenceStore
{
    // Every absence, past ones included. The table holds a few rows per person per year.
    public static async Task<List<AbsenceSnapshot>> SnapshotsAsync(AppDbContext db, CancellationToken ct) =>
        await db.Absences
            .AsNoTracking()
            .Select(a => new AbsenceSnapshot(a.Id, a.UserId, a.StartDate, a.EndDate, a.CoverUserId))
            .ToListAsync(ct);
}
