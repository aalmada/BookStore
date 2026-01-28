using BookStore.ApiService.Events;
using BookStore.ApiService.Infrastructure;
using BookStore.Shared.Models;

namespace BookStore.ApiService.Aggregates;

public record SaleAggregate
{
    public Guid Id { get; internal set; }
    public List<BookSale> ScheduledSales { get; private set; } = [];

    // Marten rehydration
    internal void Apply(BookSaleScheduled @event)
    {
        Id = @event.Id;
        // Remove any existing sale with the same start time to avoid duplicates if re-applied or creating conflict
        _ = ScheduledSales.RemoveAll(s => s.Start == @event.Sale.Start);
        ScheduledSales.Add(@event.Sale);
    }

    internal void Apply(BookSaleCancelled @event)
    {
        _ = ScheduledSales.RemoveAll(s => s.Start == @event.SaleStart);
    }

    public Result<BookSaleScheduled> ScheduleSale(decimal percentage, DateTimeOffset start, DateTimeOffset end)
    {
        if (percentage is <= 0 or >= 100)
        {
            return Result.Failure<BookSaleScheduled>(Error.Validation(ErrorCodes.Books.PriceNegative, "Sale percentage must be greater than 0 and less than 100"));
        }

        if (start >= end)
        {
            return Result.Failure<BookSaleScheduled>(Error.Validation(ErrorCodes.Books.SaleOverlap, "Sale start time must be before end time"));
        }

        // Check for overlapping sales
        if (ScheduledSales.Any(s => (start < s.End && end > s.Start)))
        {
            return Result.Failure<BookSaleScheduled>(Error.Conflict(ErrorCodes.Books.SaleOverlap, "Sale period overlaps with an existing sale"));
        }

        var sale = new BookSale(percentage, start, end);
        return new BookSaleScheduled(Id, sale);
    }

    public Result<BookSaleCancelled> CancelSale(DateTimeOffset saleStart)
    {
        var sale = ScheduledSales.FirstOrDefault(s => s.Start == saleStart);
        if (sale.Equals(default(BookSale)))
        {
            return Result.Failure<BookSaleCancelled>(Error.NotFound(ErrorCodes.Books.SaleNotFound, "No sale found with the specified start time"));
        }

        return new BookSaleCancelled(Id, saleStart);
    }
}
