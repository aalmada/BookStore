using BookStore.ApiService.Aggregates;
using BookStore.ApiService.Commands;
using BookStore.ApiService.Infrastructure;
using BookStore.ApiService.Infrastructure.Extensions;
using BookStore.ApiService.Infrastructure.Logging;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Caching.Hybrid;

namespace BookStore.ApiService.Handlers.Books;

public static class BookPriceHandlers
{
    public static async Task<IResult> Handle(
        ApplyBookDiscount command,
        IDocumentStore store,
        HybridCache cache,
        ILogger logger,
        CancellationToken ct)
    {
        await using var session = store.LightweightSession(command.TenantId);
        var aggregate = await session.Events.AggregateStreamAsync<BookAggregate>(command.BookId, token: ct);
        if (aggregate is null)
        {
            // If book not found, we can't apply discount. 
            // Since this is a background job, we log and exit.
            Log.Books.BookNotFound(logger, command.BookId);
            return Results.NotFound();
        }

        var eventResult = aggregate.ApplyDiscount(command.Percentage);
        if (eventResult.IsFailure)
        {
            Log.Books.ApplyDiscountFailed(logger, command.BookId, eventResult.Error.Message);
            return eventResult.ToProblemDetails();
        }

        _ = session.Events.Append(command.BookId, eventResult.Value);
        await session.SaveChangesAsync(ct);

        // Invalidate cache
        await cache.RemoveByTagAsync([CacheTags.BookList, CacheTags.ForItem(CacheTags.BookItemPrefix, command.BookId)], ct);

        return Results.NoContent();
    }

    public static async Task<IResult> Handle(
        RemoveBookDiscount command,
        IDocumentStore store,
        HybridCache cache,
        ILogger logger,
        CancellationToken ct)
    {
        await using var session = store.LightweightSession(command.TenantId);
        var aggregate = await session.Events.AggregateStreamAsync<BookAggregate>(command.BookId, token: ct);
        if (aggregate is null)
        {
            Log.Books.BookNotFound(logger, command.BookId);
            return Results.NotFound();
        }

        var eventResult = aggregate.RemoveDiscount();
        if (eventResult.IsFailure)
        {
            Log.Books.RemoveDiscountFailed(logger, command.BookId, eventResult.Error.Message);
            return eventResult.ToProblemDetails();
        }

        _ = session.Events.Append(command.BookId, eventResult.Value);
        await session.SaveChangesAsync(ct);

        // Invalidate cache
        await cache.RemoveByTagAsync([CacheTags.BookList, CacheTags.ForItem(CacheTags.BookItemPrefix, command.BookId)], ct);

        return Results.NoContent();
    }
}
