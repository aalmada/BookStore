using BookStore.ApiService.Aggregates;
using BookStore.ApiService.Commands;
using BookStore.ApiService.Infrastructure;
using BookStore.ApiService.Infrastructure.Logging;
using Marten;
using Microsoft.Extensions.Caching.Hybrid;

namespace BookStore.ApiService.Handlers.{Resources};

// Handler — part of Handlers/{Resources}/{Resource}Handlers.cs
public static partial class {Resource}Handlers
{
    public static async Task<IResult> Handle(
        SoftDelete{Resource} command,
        IDocumentSession session,
        HybridCache cache,
        ILogger logger)
    {
        Log.{Resources}.{Resource}Deleting(logger, command.Id);

        // Load current aggregate state
        var aggregate = await session.Events.AggregateStreamAsync<{Resource}Aggregate>(command.Id);
        if (aggregate is null)
        {
            Log.{Resources}.{Resource}NotFound(logger, command.Id);
            return Result.Failure(Error.NotFound(ErrorCodes.{Resources}.NotFound, "{Resource} not found")).ToProblemDetails();
        }

        // ETag / optimistic concurrency check
        var expectedVersion = ETagHelper.ParseETag(command.ETag);
        if (expectedVersion.HasValue && aggregate.Version != expectedVersion.Value)
            return ETagHelper.PreconditionFailed();

        // Validate and produce the domain event
        var eventResult = aggregate.SoftDeleteEvent();
        if (eventResult.IsFailure)
            return eventResult.ToProblemDetails();

        _ = session.Events.Append(command.Id, eventResult.Value);

        // Invalidate item and list caches
        await cache.RemoveByTagAsync(
            [CacheTags.{Resource}List, CacheTags.ForItem(CacheTags.{Resource}ItemPrefix, command.Id)],
            default);

        return Results.NoContent();
    }
}
