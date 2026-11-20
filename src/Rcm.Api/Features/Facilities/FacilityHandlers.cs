using Microsoft.EntityFrameworkCore;
using Rcm.Api.Auth;
using Rcm.Api.Data;
using Rcm.Api.Domain;

namespace Rcm.Api.Features.Facilities;

public sealed record FacilityRow(int Id, string Code, string Name, bool IsActive, PersonRef Owner);

// EligibleOwners fills the move picker, so it is empty for anyone who cannot move a facility.
public sealed record FacilitiesResponse(IReadOnlyList<FacilityRow> Facilities, IReadOnlyList<PersonRef> EligibleOwners);

// MoveOpenItem defaults to on, as in the move dialog.
public sealed record MoveFacilityRequest(int? NewOwnerId, string? Reason, bool MoveOpenItem = true);

// MovedItemId is the open month's item when it went with the facility.
public sealed record MoveFacilityResponse(FacilityRow Facility, int? MovedItemId);

public sealed class GetFacilitiesHandler(AppDbContext db)
{
    public async Task<Result<FacilitiesResponse>> HandleAsync(CurrentUser user, CancellationToken ct)
    {
        if (!VisibilityRules.HasOverview(user.Role))
        {
            return Error.Forbidden("Facilities are for supervisors and managers.");
        }

        var people = await FacilityQueries.PeopleAsync(db, ct);
        var names = people.ToDictionary(p => p.Id, p => p.Name);
        var facilities = await db.Facilities.AsNoTracking().OrderBy(f => f.Code).ToListAsync(ct);

        return new FacilitiesResponse(
            facilities.Select(f => FacilityQueries.Row(f, names)).ToList(),
            FacilityMoveRules.MayMove(user.Role)
                ? FacilityMoveRules.EligibleOwners(people).Select(p => new PersonRef(p.Id, p.Name)).ToList()
                : []);
    }
}

// Ownership, the assignment log and the open item change in one SaveChanges, so in one transaction.
public sealed class MoveFacilityHandler(AppDbContext db, BusinessCalendar calendar)
{
    public async Task<Result<MoveFacilityResponse>> HandleAsync(
        CurrentUser user, int facilityId, MoveFacilityRequest request, CancellationToken ct)
    {
        if (!FacilityMoveRules.MayMove(user.Role))
        {
            return Error.Forbidden("Only a manager can move a facility.");
        }

        var facility = await db.Facilities.SingleOrDefaultAsync(f => f.Id == facilityId, ct);
        if (facility is null)
        {
            return Error.NotFound("Facility not found.");
        }

        var people = await FacilityQueries.PeopleAsync(db, ct);
        var decision = FacilityMoveRules.CanMove(
            user.Role,
            facility.IsActive,
            facility.OwnerId,
            people.SingleOrDefault(p => p.Id == request.NewOwnerId),
            request.Reason);
        if (!decision.IsSuccess)
        {
            return decision.Error;
        }

        var current = await db.FacilityAssignments
            .SingleOrDefaultAsync(a => a.FacilityId == facilityId && a.EndedAt == null, ct);
        var openItem = request.MoveOpenItem
            ? await db.WorkItems.SingleOrDefaultAsync(
                w => w.FacilityId == facilityId && w.Cycle!.Status == CycleStatus.Open, ct)
            : null;

        var result = FacilityMoveRules.Apply(facility, current, decision.Value, openItem, user.Id, calendar.UtcNow);
        db.FacilityAssignments.Add(result.Started);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // The open item changed between our read and our write. Nothing was saved.
            return Error.Conflict("This month's item changed during the move. Reload and try again.");
        }
        catch (DbUpdateException)
        {
            // The one current assignment index caught another move of this facility at the same time.
            return Error.Conflict("Someone else moved this facility at the same time. Reload and try again.");
        }

        return new MoveFacilityResponse(
            FacilityQueries.Row(facility, people.ToDictionary(p => p.Id, p => p.Name)),
            result.MovedItemEvent is null ? null : openItem!.Id);
    }
}

internal static class FacilityQueries
{
    public static async Task<List<OrgPerson>> PeopleAsync(AppDbContext db, CancellationToken ct) =>
        await db.Users
            .AsNoTracking()
            .Select(u => new OrgPerson(u.Id, u.Name, u.Role, u.ReportsToId, u.IsActive))
            .ToListAsync(ct);

    public static FacilityRow Row(Facility facility, IReadOnlyDictionary<int, string> names) =>
        new(
            facility.Id,
            facility.Code,
            facility.Name,
            facility.IsActive,
            new PersonRef(facility.OwnerId, names.GetValueOrDefault(facility.OwnerId, $"User {facility.OwnerId}")));
}
