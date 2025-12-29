namespace BookStore.ApiService.Models;

/// <summary>
/// Represents a partial date that can be year-only, year-month, or a complete date.
/// Useful for publication dates where the exact date may not be known.
/// </summary>
public readonly record struct PartialDate : IComparable<PartialDate>
{
    /// <summary>
    /// The year component (required)
    /// </summary>
    public int Year { get; }

    /// <summary>
    /// The month component (optional, 1-12)
    /// </summary>
    public int? Month { get; }

    /// <summary>
    /// The day component (optional, 1-31)
    /// </summary>
    public int? Day { get; }

    /// <summary>
    /// Creates a partial date with only a year
    /// </summary>
    public PartialDate(int year)
    {
        ValidateYear(year);
        Year = year;
        Month = null;
        Day = null;
    }

    /// <summary>
    /// Creates a partial date with year and month
    /// </summary>
    public PartialDate(int year, int month)
    {
        ValidateYear(year);
        ValidateMonth(month);
        Year = year;
        Month = month;
        Day = null;
    }

    /// <summary>
    /// Creates a complete date with year, month, and day
    /// </summary>
    public PartialDate(int year, int month, int day)
    {
        // Use DateOnly for validation when we have a complete date
        // This handles leap years and days per month automatically
        _ = new DateOnly(year, month, day);

        Year = year;
        Month = month;
        Day = day;
    }

    static void ValidateYear(int year)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(year, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(year, 9999);
    }

    static void ValidateMonth(int month)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(month, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(month, 12);
    }

    /// <summary>
    /// Converts to DateOnly if this is a complete date, otherwise returns null
    /// </summary>
    public DateOnly? ToDateOnly()
    {
        if (Day.HasValue && Month.HasValue)
        {
            return new DateOnly(Year, Month.Value, Day.Value);
        }

        return null;
    }

    /// <summary>
    /// Creates a PartialDate from a DateOnly
    /// </summary>
    public static PartialDate FromDateOnly(DateOnly date) => new(date.Year, date.Month, date.Day);

    /// <summary>
    /// Returns the string representation in ISO 8601 format
    /// </summary>
    public override string ToString()
    {
        if (Day.HasValue && Month.HasValue)
        {
            return $"{Year:D4}-{Month.Value:D2}-{Day.Value:D2}";
        }

        if (Month.HasValue)
        {
            return $"{Year:D4}-{Month.Value:D2}";
        }

        return $"{Year:D4}";
    }

    /// <summary>
    /// Compares two partial dates for sorting
    /// </summary>
    public int CompareTo(PartialDate other)
    {
        var yearComparison = Year.CompareTo(other.Year);
        if (yearComparison != 0)
        {
            return yearComparison;
        }

        // If years are equal, compare months
        var thisMonth = Month ?? 0;
        var otherMonth = other.Month ?? 0;
        var monthComparison = thisMonth.CompareTo(otherMonth);
        if (monthComparison != 0)
        {
            return monthComparison;
        }

        // If months are equal, compare days
        var thisDay = Day ?? 0;
        var otherDay = other.Day ?? 0;
        return thisDay.CompareTo(otherDay);
    }

    public static bool operator <(PartialDate left, PartialDate right) => left.CompareTo(right) < 0;
    public static bool operator >(PartialDate left, PartialDate right) => left.CompareTo(right) > 0;
    public static bool operator <=(PartialDate left, PartialDate right) => left.CompareTo(right) <= 0;
    public static bool operator >=(PartialDate left, PartialDate right) => left.CompareTo(right) >= 0;
}
