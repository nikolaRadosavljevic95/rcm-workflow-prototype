using Rcm.Api.Domain;
using Rcm.Api.Tests.TestSupport;

namespace Rcm.Api.Tests.Domain;

public class BusinessCalendarTests
{
    private static BusinessCalendar At(int year, int month, int day, int hour, int minute) =>
        new(new FixedTimeProvider(new DateTimeOffset(year, month, day, hour, minute, 0, TimeSpan.Zero)), SqliteTestDatabase.NewYork);

    [Fact]
    public void Today_UtcPastMidnightBeforeNewYorkMidnight_ReturnsPreviousDay()
    {
        Assert.Equal(new DateOnly(2026, 11, 19), At(2026, 11, 20, 4, 59).Today);
    }

    [Fact]
    public void Today_UtcPastNewYorkMidnightInWinter_ReturnsSameDay()
    {
        Assert.Equal(new DateOnly(2026, 11, 20), At(2026, 11, 20, 5, 0).Today);
    }

    [Fact]
    public void Today_SummerTime_UsesDaylightOffset()
    {
        Assert.Equal(new DateOnly(2026, 6, 30), At(2026, 7, 1, 3, 59).Today);
        Assert.Equal(new DateOnly(2026, 7, 1), At(2026, 7, 1, 4, 0).Today);
    }

    [Fact]
    public void ToUtc_BusinessMorning_AppliesOffsetForThatDate()
    {
        var calendar = At(2026, 11, 19, 12, 0);

        Assert.Equal(new DateTime(2026, 11, 19, 13, 0, 0, DateTimeKind.Utc), calendar.ToUtc(new DateOnly(2026, 11, 19), new TimeOnly(8, 0)));
        Assert.Equal(new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc), calendar.ToUtc(new DateOnly(2026, 7, 1), new TimeOnly(8, 0)));
    }

    [Fact]
    public void DateOf_UtcTimestamp_ReturnsBusinessDate()
    {
        var calendar = At(2026, 11, 19, 12, 0);

        Assert.Equal(new DateOnly(2026, 11, 18), calendar.DateOf(new DateTime(2026, 11, 19, 2, 0, 0, DateTimeKind.Utc)));
    }
}
