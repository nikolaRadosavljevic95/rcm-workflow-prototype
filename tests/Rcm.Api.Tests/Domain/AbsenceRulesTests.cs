using Rcm.Api.Domain;
using static Rcm.Api.Tests.TestSupport.TestOrg;

namespace Rcm.Api.Tests.Domain;

public class AbsenceRulesTests
{
    private static readonly DateOnly Today = new(2026, 11, 19);

    // Blake is away Nov 17 to 23, covered by Alex.
    private static readonly AbsenceSnapshot BlakeAway = new(1, Blake, Today.AddDays(-2), Today.AddDays(4), Alex);

    [Fact]
    public void Validate_NewAbsenceInFutureWithCover_Succeeds()
    {
        var result = Validate(Change(Dana, 1, 3, cover: Jordan, note: "  Annual leave  "));

        Assert.True(result.IsSuccess);
        Assert.Equal("Annual leave", result.Value.Note);
    }

    [Fact]
    public void Validate_WithoutCover_Succeeds()
    {
        Assert.True(Validate(Change(Dana, 0, 2, cover: null)).IsSuccess);
    }

    [Fact]
    public void Validate_StartsBeforeToday_ReturnsInvalid()
    {
        Assert.Equal(ErrorKind.Invalid, Validate(Change(Dana, -1, 2, cover: null)).Error?.Kind);
    }

    [Fact]
    public void Validate_EndBeforeStart_ReturnsInvalid()
    {
        Assert.Equal(ErrorKind.Invalid, Validate(Change(Dana, 3, 2, cover: null)).Error?.Kind);
    }

    [Fact]
    public void Validate_OverlapsOwnAbsence_ReturnsInvalid()
    {
        var result = Validate(Change(Blake, 4, 6, cover: null), absences: [BlakeAway]);

        Assert.Equal("Blake is already away from Nov 17 to Nov 23.", result.Error?.Message);
    }

    [Fact]
    public void Validate_SelfAsCover_ReturnsInvalid()
    {
        Assert.Equal(ErrorKind.Invalid, Validate(Change(Dana, 1, 2, cover: Dana)).Error?.Kind);
    }

    [Fact]
    public void Validate_InactiveCoverOrPerson_ReturnsInvalid()
    {
        var org = new OrgChart(
        [
            new OrgPerson(Jordan, "Jordan", Role.Supervisor, Quinn),
            new OrgPerson(Dana, "Dana", Role.Biller, Jordan),
            new OrgPerson(Emery, "Emery", Role.Biller, Casey, IsActive: false),
        ]);

        Assert.Equal("Emery is inactive.", AbsenceRules.Validate(Change(Dana, 1, 2, cover: Emery), null, Today, org, []).Error?.Message);
        Assert.Equal("Emery is inactive.", AbsenceRules.Validate(Change(Emery, 1, 2, cover: null), null, Today, org, []).Error?.Message);
    }

    [Fact]
    public void Validate_CoverAwayOnOneOfTheDays_NamesTheFirstDay()
    {
        var result = Validate(Change(Charlie, 3, 6, cover: Blake), absences: [BlakeAway]);

        Assert.Equal("Blake is away on Nov 22.", result.Error?.Message);
    }

    [Fact]
    public void Validate_BillerCoversSupervisor_ReturnsInvalid()
    {
        var result = Validate(Change(Casey, 1, 2, cover: Emery));

        Assert.Equal(ErrorKind.Invalid, result.Error?.Kind);
        Assert.Contains("supervisor or higher", result.Error?.Message);
    }

    [Fact]
    public void Validate_SupervisorCoversBillerOrPeer_Succeeds()
    {
        Assert.True(Validate(Change(Dana, 1, 2, cover: Jordan)).IsSuccess);
        Assert.True(Validate(Change(Casey, 1, 2, cover: Jordan)).IsSuccess);
    }

    [Fact]
    public void Validate_NoteTooLong_ReturnsInvalid()
    {
        var note = new string('x', AbsenceRules.MaxNoteLength + 1);

        Assert.Equal(ErrorKind.Invalid, Validate(Change(Dana, 1, 2, cover: null, note: note)).Error?.Kind);
    }

    [Fact]
    public void Validate_StartedAbsenceNewEndOrCover_Succeeds()
    {
        var shorter = Change(Blake, -2, 0, cover: Alex);
        var longer = Change(Blake, -2, 8, cover: Sam);

        Assert.True(Validate(shorter, existing: BlakeAway, absences: [BlakeAway]).IsSuccess);
        Assert.True(Validate(longer, existing: BlakeAway, absences: [BlakeAway]).IsSuccess);
    }

    [Fact]
    public void Validate_StartedAbsenceStartMoved_ReturnsInvalid()
    {
        var moved = Change(Blake, -1, 4, cover: Alex);

        Assert.Equal(ErrorKind.Invalid, Validate(moved, existing: BlakeAway, absences: [BlakeAway]).Error?.Kind);
    }

    [Fact]
    public void Validate_StartedAbsenceEndBeforeToday_ReturnsInvalid()
    {
        var ended = Change(Blake, -2, -1, cover: Alex);

        Assert.Equal(ErrorKind.Invalid, Validate(ended, existing: BlakeAway, absences: [BlakeAway]).Error?.Kind);
    }

    [Fact]
    public void Validate_EndedAbsence_ReturnsInvalid()
    {
        var past = new AbsenceSnapshot(2, Dana, Today.AddDays(-9), Today.AddDays(-5), null);

        var result = Validate(Change(Dana, -9, -5, cover: Jordan), existing: past, absences: [past]);

        Assert.Equal("This absence has ended and can no longer change.", result.Error?.Message);
    }

    [Fact]
    public void Validate_UpcomingAbsenceMovedLater_Succeeds()
    {
        var upcoming = new AbsenceSnapshot(2, Dana, Today.AddDays(3), Today.AddDays(5), null);

        Assert.True(Validate(Change(Dana, 6, 8, cover: null), existing: upcoming, absences: [upcoming]).IsSuccess);
    }

    [Fact]
    public void MayManage_ByRole_ManagerAllSupervisorTeamBillerNone()
    {
        Assert.True(AbsenceRules.MayManage(Actor(Morgan), Emery, Chart));
        Assert.True(AbsenceRules.MayManage(Actor(Jordan), Jordan, Chart));
        Assert.True(AbsenceRules.MayManage(Actor(Jordan), Dana, Chart));
        Assert.False(AbsenceRules.MayManage(Actor(Jordan), Alex, Chart));
        Assert.False(AbsenceRules.MayManage(Actor(Alex), Alex, Chart));
    }

    [Fact]
    public void CanView_SupervisorAsCover_SeesButCannotEdit()
    {
        var taylorAway = new AbsenceSnapshot(2, Taylor, Today.AddDays(-1), Today.AddDays(3), Sam);

        Assert.True(AbsenceRules.CanView(Actor(Sam), taylorAway, Chart));
        Assert.False(AbsenceRules.CanEdit(Actor(Sam), taylorAway, Chart, Today));
        Assert.False(AbsenceRules.CanView(Actor(Alex), BlakeAway, Chart));
    }

    [Fact]
    public void CanDelete_BeforeStartOnly()
    {
        var upcoming = new AbsenceSnapshot(2, Dana, Today.AddDays(1), Today.AddDays(2), null);
        var startsToday = new AbsenceSnapshot(3, Dana, Today, Today.AddDays(2), null);

        Assert.True(AbsenceRules.CanDelete(Actor(Jordan), upcoming, Chart, Today));
        Assert.False(AbsenceRules.CanDelete(Actor(Jordan), startsToday, Chart, Today));
        Assert.False(AbsenceRules.CanDelete(Actor(Sam), BlakeAway, Chart, Today));
    }

    [Fact]
    public void TodayState_ByDatesAndCover_DescribesToday()
    {
        var upcoming = new AbsenceSnapshot(2, Dana, Today.AddDays(1), Today.AddDays(2), null);
        var uncovered = new AbsenceSnapshot(3, Charlie, Today, Today, null);
        var ended = new AbsenceSnapshot(4, Emery, Today.AddDays(-3), Today.AddDays(-1), null);
        AbsenceSnapshot[] all = [BlakeAway, upcoming, uncovered, ended];

        Assert.Equal(AbsenceToday.AwayCovered, AbsenceRules.TodayState(BlakeAway, Today, all, Chart));
        Assert.Equal(AbsenceToday.Upcoming, AbsenceRules.TodayState(upcoming, Today, all, Chart));
        Assert.Equal(AbsenceToday.AwayNeedsCover, AbsenceRules.TodayState(uncovered, Today, all, Chart));
        Assert.Equal(AbsenceToday.Ended, AbsenceRules.TodayState(ended, Today, all, Chart));
    }

    private static AbsenceChange Change(int userId, int fromDays, int toDays, int? cover, string? note = null) =>
        new(userId, Today.AddDays(fromDays), Today.AddDays(toDays), cover, note);

    private static Result<AbsenceChange> Validate(
        AbsenceChange change, AbsenceSnapshot? existing = null, AbsenceSnapshot[]? absences = null) =>
        AbsenceRules.Validate(change, existing, Today, Chart, absences ?? []);
}
