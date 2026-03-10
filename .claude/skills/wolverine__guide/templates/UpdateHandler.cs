using BookStore.ApiService.Aggregates;
using BookStore.ApiService.Commands;
using BookStore.ApiService.Infrastructure;
using Marten;
using Microsoft.Extensions.Caching.Hybrid;

namespace BookStore.ApiService.Handlers.{Resources};

// Handler — part of Handlers/{Resources}/{Resource}Handlers.cs
public static partial class {Resource}Handlers
{
    public static async Task<IResult> Handle(
        Update{Resource} command,
        IDocumentSession session,
        HybridCache cache,
        CancellationToken cancellationToken)
    {
        // Load current aggregate state
        var aggregate = await session.Events.AggregateStreamAsync<{Resource}Aggregate>(command.Id);
        if (aggregate is null)
            return Result.Failure(Error.NotFound(ErrorCodes.{Resources}.NotFound, "{Resource} not found")).ToProblemDetails();

        // ETag / optimistic concurrency check
        var expectedVersion = ETagHelper.ParseETag(command.ETag);
        if (expectedVersion.HasValue && aggregate.Version != expectedVersion.Value)
            return ETagHelper.PreconditionFailed();

        // Validate and produce the domain event
        var eventResult = aggregate.UpdateEvent(command.Name /*, other args */);
        if (eventResult.IsFailure)
            return eventResult.ToProblemDetails();

        _ = session.Events.Append(command.Id, eventResult.Value);

        // Invalidate item and list caches
        await cache.RemoveByTagAsync(
            [CacheTags.{Resource}List, CacheTags.ForItem(CacheTags.{Resource}ItemPrefix, command.Id)],
            cancellationToken);

        return Results.NoContent();
    }
}
