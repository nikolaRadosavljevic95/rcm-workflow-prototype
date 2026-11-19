namespace Rcm.Api.Domain;

// Business dates follow the configured time zone, never the server's.
public sealed class BusinessCalendar(TimeProvider timeProvider, TimeZoneInfo timeZone)
{
    public DateTime UtcNow => timeProvider.GetUtcNow().UtcDateTime;

    public DateOnly Today => DateOf(UtcNow);

    public DateOnly DateOf(DateTime utc) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, timeZone));

    public DateTime ToUtc(DateOnly date, TimeOnly time) =>
        TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(time, DateTimeKind.Unspecified), timeZone);
}
