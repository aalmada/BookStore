namespace BookStore.Shared.Models;

/// <summary>
/// Represents a scheduled sale for a book with a percentage discount and time range.
/// </summary>
public readonly record struct BookSale
{
    /// <summary>
    /// Discount percentage (0 < Percentage < 100)
    /// </summary>
    /// <summary>
    /// Discount percentage (0 < Percentage < 100)
    /// </summary>
    public decimal Percentage { get; init; }

    /// <summary>
    /// Sale start time (UTC)
    /// </summary>
    public DateTimeOffset Start { get; init; }

    /// <summary>
    /// Sale end time (UTC)
    /// </summary>
    public DateTimeOffset End { get; init; }

    public BookSale(decimal percentage, DateTimeOffset start, DateTimeOffset end)
    {
        if (percentage is <= 0 or >= 100)
        {
            throw new ArgumentException("Sale percentage must be greater than 0 and less than 100", nameof(percentage));
        }

        if (start >= end)
        {
            throw new ArgumentException("Sale start time must be before end time", nameof(start));
        }

        Percentage = percentage;
        Start = start;
        End = end;
    }

    /// <summary>
    /// Checks if the sale is currently active
    /// </summary>
    public bool IsActive(DateTimeOffset now) 
        => now >= Start && now < End;

    /// <summary>
    /// Calculates the discounted price
    /// </summary>
    public decimal CalculateDiscountedPrice(decimal originalPrice) 
        => originalPrice * (1 - Percentage / 100);
}
