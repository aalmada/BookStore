using BookStore.Shared.Models;

namespace BookStore.ApiService.Commands;

/// <summary>
/// Command to schedule a sale for a book via SaleAggregate
/// </summary>
public record ScheduleSale(
    Guid BookId,
    decimal Percentage,
    DateTimeOffset Start,
    DateTimeOffset End)
{
    public string? ETag { get; init; }
}

/// <summary>
/// Command to cancel a scheduled sale via SaleAggregate
/// </summary>
public record CancelSale(
    Guid BookId,
    DateTimeOffset SaleStart)
{
    public string? ETag { get; init; }
}
