namespace Rcm.Api.Domain;

public readonly record struct YearMonth(int Year, int Month)
{
    public static YearMonth Of(DateOnly date) => new(date.Year, date.Month);

    public YearMonth Next() => Month == 12 ? new(Year + 1, 1) : new(Year, Month + 1);

    public bool Contains(DateOnly date) => date.Year == Year && date.Month == Month;

    public DateOnly Day(int day) => new(Year, Month, day);
}
