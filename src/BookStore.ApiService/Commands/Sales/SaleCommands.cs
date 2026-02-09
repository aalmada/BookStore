using BookStore.Shared.Commands;

namespace BookStore.ApiService.Commands;

/// <summary>
/// Command to schedule a sale for a book via SaleAggregate
/// </summary>
public record ScheduleSale(
    Guid BookId,
    decimal Percentage,
    DateTimeOffset Start,
    DateTimeOffset End) : IHaveETag
{
    public string? ETag { get; set; }
}

/// <summary>
/// Command to cancel a scheduled sale via SaleAggregate
/// </summary>
public record CancelSale(
    Guid BookId,
    DateTimeOffset SaleStart) : IHaveETag
{
    public string? ETag { get; set; }
}
