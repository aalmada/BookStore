using BookStore.ApiService.Aggregates;
using BookStore.ApiService.Commands;
using BookStore.ApiService.Infrastructure;
using BookStore.ApiService.Infrastructure.Extensions;
using BookStore.ApiService.Infrastructure.Logging;
using Marten;
using Microsoft.Extensions.Caching.Hybrid;

namespace BookStore.ApiService.Handlers.Publishers;

public static class PublisherHandlers
{
    public static async Task<IResult> Handle(
        CreatePublisher command,
        IDocumentSession session,
        HybridCache cache,
        ILogger logger)
    {
        Log.Publishers.PublisherCreating(logger, command.Id, command.Name, session.CorrelationId ?? "none");

        var eventResult = PublisherAggregate.CreateEvent(command.Id, command.Name);
        if (eventResult.IsFailure)
        {
            return eventResult.ToProblemDetails();
        }

        _ = session.Events.StartStream<PublisherAggregate>(command.Id, eventResult.Value);

        Log.Publishers.PublisherCreated(logger, command.Id, command.Name);

        // Invalidate cache
        await cache.RemoveByTagAsync([CacheTags.PublisherList], default);

        return Results.Created(
            $"/api/admin/publishers/{command.Id}",
            new { id = command.Id, correlationId = session.CorrelationId });
    }

    public static async Task<IResult> Handle(
        UpdatePublisher command,
        IDocumentSession session,
        IHttpContextAccessor httpContextAccessor,
        HybridCache cache,
        ILogger logger)
    {
        var aggregate = await session.Events.AggregateStreamAsync<PublisherAggregate>(command.Id);
        if (aggregate is null)
        {
            Log.Publishers.PublisherNotFound(logger, command.Id);
            return Result.Failure(Error.NotFound(ErrorCodes.Publishers.NotDeleted, "Publisher not found")).ToProblemDetails();
        }

        Log.Publishers.PublisherUpdating(logger, command.Id, command.Name, aggregate.Version);

        var expectedVersion = ETagHelper.ParseETag(command.ETag);
        if (expectedVersion.HasValue && aggregate.Version != expectedVersion.Value)
        {
             return ETagHelper.PreconditionFailed();
        }

        var eventResult = aggregate.UpdateEvent(command.Name);
        if (eventResult.IsFailure)
        {
            return eventResult.ToProblemDetails();
        }

        _ = session.Events.Append(command.Id, eventResult.Value);

        // Invalidate cache
        await cache.RemoveByTagAsync([CacheTags.PublisherList, CacheTags.ForItem(CacheTags.PublisherItemPrefix, command.Id)], default);

        return Results.NoContent();
    }

    public static async Task<IResult> Handle(
        SoftDeletePublisher command,
        IDocumentSession session,
        IHttpContextAccessor httpContextAccessor,
        HybridCache cache,
        ILogger logger)
    {
        Log.Publishers.PublisherSoftDeleting(logger, command.Id);

        var aggregate = await session.Events.AggregateStreamAsync<PublisherAggregate>(command.Id);
        if (aggregate is null)
        {
            Log.Publishers.PublisherNotFound(logger, command.Id);
            return Result.Failure(Error.NotFound(ErrorCodes.Publishers.NotDeleted, "Publisher not found")).ToProblemDetails();
        }

        var expectedVersion = ETagHelper.ParseETag(command.ETag);
        if (expectedVersion.HasValue && aggregate.Version != expectedVersion.Value)
        {
             return ETagHelper.PreconditionFailed();
        }

        var eventResult = aggregate.SoftDeleteEvent();
        if (eventResult.IsFailure)
        {
            return eventResult.ToProblemDetails();
        }

        _ = session.Events.Append(command.Id, eventResult.Value);

        // Invalidate cache
        await cache.RemoveByTagAsync([CacheTags.PublisherList, CacheTags.ForItem(CacheTags.PublisherItemPrefix, command.Id)], default);

        return Results.NoContent();
    }

    public static async Task<IResult> Handle(
        RestorePublisher command,
        IDocumentSession session,
        IHttpContextAccessor httpContextAccessor,
        HybridCache cache,
        ILogger logger)
    {
        Log.Publishers.PublisherRestoring(logger, command.Id);

        var aggregate = await session.Events.AggregateStreamAsync<PublisherAggregate>(command.Id);
        if (aggregate is null)
        {
            Log.Publishers.PublisherNotFound(logger, command.Id);
            return Result.Failure(Error.NotFound(ErrorCodes.Publishers.NotDeleted, "Publisher not found")).ToProblemDetails();
        }

        var expectedVersion = ETagHelper.ParseETag(command.ETag);
        if (expectedVersion.HasValue && aggregate.Version != expectedVersion.Value)
        {
             return ETagHelper.PreconditionFailed();
        }

        var eventResult = aggregate.RestoreEvent();
        if (eventResult.IsFailure)
        {
            return eventResult.ToProblemDetails();
        }

        _ = session.Events.Append(command.Id, eventResult.Value);

        // Invalidate cache
        await cache.RemoveByTagAsync([CacheTags.PublisherList, CacheTags.ForItem(CacheTags.PublisherItemPrefix, command.Id)], default);

        return Results.NoContent();
    }
}
