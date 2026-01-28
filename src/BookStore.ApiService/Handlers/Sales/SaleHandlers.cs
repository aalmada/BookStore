using BookStore.ApiService.Aggregates;
using BookStore.ApiService.Commands;
using BookStore.ApiService.Events;
using BookStore.ApiService.Infrastructure;
using BookStore.ApiService.Infrastructure.Extensions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using Wolverine;

namespace BookStore.ApiService.Handlers.Sales;

public static class SaleHandlers
{
    public static async Task<IResult> Handle(
        ScheduleSale command,
        IDocumentSession session)
    {
        try
        {
            var streamState = await session.Events.FetchStreamStateAsync(command.BookId);
            if (streamState == null)
            {
                return Results.NotFound();
            }

            // Manually project SaleAggregate to avoid Marten exceptions on unknown events
            var events = await session.Events.FetchStreamAsync(command.BookId);
            var aggregate = new SaleAggregate { Id = command.BookId };
            foreach (var e in events)
            {
                if (e.Data is BookSaleScheduled s)
                {
                    aggregate.Apply(s);
                }

                if (e.Data is BookSaleCancelled c)
                {
                    aggregate.Apply(c);
                }
            }

            var result = aggregate.ScheduleSale(command.Percentage, command.Start, command.End);
            if (result.IsFailure)
            {
                return result.ToProblemDetails();
            }

            // Append with expected version to force Marten to recognize existing stream
            _ = session.Events.Append(command.BookId, streamState.Version + 1, result.Value);
            await session.SaveChangesAsync();
            return Results.NoContent();
        }
        catch
        {
            throw;
        }
    }

    public static async Task<IResult> Handle(
        CancelSale command,
        IDocumentSession session)
    {
        var streamState = await session.Events.FetchStreamStateAsync(command.BookId);
        if (streamState == null)
        {
            return Results.NotFound();
        }

        // Manually project SaleAggregate
        var events = await session.Events.FetchStreamAsync(command.BookId);
        var aggregate = new SaleAggregate();
        foreach (var e in events)
        {
            if (e.Data is BookSaleScheduled s)
            {
                aggregate.Apply(s);
            }

            if (e.Data is BookSaleCancelled c)
            {
                aggregate.Apply(c);
            }
        }

        var eventResult = aggregate.CancelSale(command.SaleStart);
        if (eventResult.IsFailure)
        {
            return eventResult.ToProblemDetails();
        }

        _ = session.Events.Append(command.BookId, streamState.Version + 1, eventResult.Value);
        await session.SaveChangesAsync();

        return Results.NoContent();
    }

    // React to BookSaleScheduled event to schedule future commands
    public static async Task Handle(
        JasperFx.Events.IEvent<BookSaleScheduled> wrapper,
        IMessageContext bus)
    {
        var tenantId = wrapper.TenantId ?? "";
        var @event = wrapper.Data;
        // Schedule 'ApplyBookDiscount' at Start time
        await bus.ScheduleAsync(
            new ApplyBookDiscount(@event.Id, @event.Sale.Percentage, tenantId),
            @event.Sale.Start);

        // Schedule 'RemoveBookDiscount' at End time
        await bus.ScheduleAsync(
            new RemoveBookDiscount(@event.Id, tenantId),
            @event.Sale.End);
    }
}
