using BookStore.ApiService.Aggregates;
using BookStore.ApiService.Commands;
using BookStore.ApiService.Events;
using BookStore.ApiService.Infrastructure;
using BookStore.ApiService.Infrastructure.Extensions;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Caching.Hybrid;
using Wolverine;

namespace BookStore.ApiService.Handlers.Sales;

public static class SaleHandlers
{
    public static async Task<IResult> Handle(
        ScheduleSale command,
        IDocumentSession session,
        HybridCache cache)
    {
        Instrumentation.SalesScheduled.Add(1, new System.Diagnostics.TagList { { "tenant_id", session.TenantId } });
        
        // Manually project SaleAggregate to avoid Marten exceptions on unknown events
        var events = await session.Events.FetchStreamAsync(command.BookId);
        if (events == null || events.Count == 0)
        {
            return Results.NotFound();
        }

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

        var currentVersion = events.Max(e => e.Version);
        var expectedVersion = ETagHelper.ParseETag(command.ETag);
        if (expectedVersion.HasValue)
        {
            if (currentVersion != expectedVersion.Value)
            {
                // Diagnostic logging
                try { System.IO.File.AppendAllText("/Users/antaoalmada/Projects/BookStore/debug_concurrency_sales.log", $"[{DateTimeOffset.UtcNow}] ScheduleSale Mismatch: BookId={command.BookId} Expected={expectedVersion.Value} Actual={currentVersion} ETagHeader={command.ETag}\n"); } catch { }
                return ETagHelper.PreconditionFailed();
            }

            _ = session.Events.Append(command.BookId, result.Value);
        }
        else
        {
            _ = session.Events.Append(command.BookId, result.Value);
        }

        // Invalidate cache immediately
        await cache.RemoveByTagAsync([CacheTags.BookList, CacheTags.ForItem(CacheTags.BookItemPrefix, command.BookId)], default);
        
        return Results.NoContent();
    }

    public static async Task<IResult> Handle(
        CancelSale command,
        IDocumentSession session,
        HybridCache cache)
    {
        Instrumentation.SalesCanceled.Add(1, new System.Diagnostics.TagList { { "tenant_id", session.TenantId } });
        
        // Manually project SaleAggregate
        var events = await session.Events.FetchStreamAsync(command.BookId);
        if (events == null || events.Count == 0)
        {
            return Results.NotFound();
        }

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

        var eventResult = aggregate.CancelSale(command.SaleStart);
        if (eventResult.IsFailure)
        {
            return eventResult.ToProblemDetails();
        }

        _ = session.Events.Append(command.BookId, eventResult.Value);

        // Invalidate cache immediately
        await cache.RemoveByTagAsync([CacheTags.BookList, CacheTags.ForItem(CacheTags.BookItemPrefix, command.BookId)], default);

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
