using Rcm.Api.Domain;
using static Rcm.Api.Tests.TestSupport.TestOrg;

namespace Rcm.Api.Tests.Domain;

public class CoverRulesTests
{
    private static readonly DateOnly Today = new(2026, 11, 19);

    [Fact]
    public void StateOf_NotAway_IsPresent()
    {
        Assert.Equal(CoverState.Present, CoverRules.StateOf(Alex, Today, [], Chart));
    }

    [Fact]
    public void StateOf_AwayWithCover_IsCoveredByThem()
    {
        AbsenceSnapshot[] absences = [Away(Blake, -2, 4, cover: Alex)];

        Assert.Equal(CoverState.CoveredBy(Alex), CoverRules.StateOf(Blake, Today, absences, Chart));
    }

    [Fact]
    public void StateOf_AwayWithoutCover_NeedsCover()
    {
        AbsenceSnapshot[] absences = [Away(Dana, 0, 2, cover: null)];

        Assert.Equal(CoverState.NeedsCover, CoverRules.StateOf(Dana, Today, absences, Chart));
    }

    [Fact]
    public void StateOf_CoverAlsoAwayThatDay_NeedsCoverWithoutChain()
    {
        AbsenceSnapshot[] absences = [Away(Blake, -2, 4, cover: Alex), Away(Alex, 0, 0, cover: Charlie, id: 2)];

        Assert.Equal(CoverState.NeedsCover, CoverRules.StateOf(Blake, Today, absences, Chart));
        Assert.DoesNotContain(Blake, CoverRules.CoversToday(Alex, Today, absences, Chart));
        Assert.DoesNotContain(Blake, CoverRules.CoversToday(Charlie, Today, absences, Chart));
    }

    [Fact]
    public void StateOf_InactiveCover_NeedsCover()
    {
        var org = new OrgChart([new OrgPerson(Alex, "Alex", Role.Biller, Sam, IsActive: false), new OrgPerson(Blake, "Blake", Role.Biller, Sam)]);
        AbsenceSnapshot[] absences = [Away(Blake, -2, 4, cover: Alex)];

        Assert.Equal(CoverState.NeedsCover, CoverRules.StateOf(Blake, Today, absences, org));
    }

    [Fact]
    public void StateOf_LastDayAndDayAfter_CoverEndsOnItsOwn()
    {
        AbsenceSnapshot[] absences = [Away(Blake, -2, 0, cover: Alex)];

        Assert.Equal(CoverState.CoveredBy(Alex), CoverRules.StateOf(Blake, Today, absences, Chart));
        Assert.Equal(CoverState.Present, CoverRules.StateOf(Blake, Today.AddDays(1), absences, Chart));
    }

    [Fact]
    public void CoversToday_ValidCover_ListsTheOwners()
    {
        AbsenceSnapshot[] absences = [Away(Blake, -2, 4, cover: Alex), Away(Taylor, -1, 3, cover: Sam, id: 2)];

        Assert.Equal([Blake], CoverRules.CoversToday(Alex, Today, absences, Chart));
        Assert.Equal([Taylor], CoverRules.CoversToday(Sam, Today, absences, Chart));
        Assert.Empty(CoverRules.CoversToday(Alex, Today.AddDays(5), absences, Chart));
    }

    private static AbsenceSnapshot Away(int userId, int fromDays, int toDays, int? cover, int id = 1) =>
        new(id, userId, Today.AddDays(fromDays), Today.AddDays(toDays), cover);
}
