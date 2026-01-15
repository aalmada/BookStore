using BookStore.ApiService.Aggregates;
using BookStore.ApiService.Commands;
using BookStore.ApiService.Infrastructure;
using BookStore.ApiService.Infrastructure.Logging;
using Marten;

namespace BookStore.ApiService.Handlers.Publishers;

public static class PublisherHandlers
{
    public static IResult Handle(CreatePublisher command, IDocumentSession session, ILogger logger)
    {
        Log.Publishers.PublisherCreating(logger, command.Id, command.Name, session.CorrelationId ?? "none");

        var @event = PublisherAggregate.CreateEvent(command.Id, command.Name);

        _ = session.Events.StartStream<PublisherAggregate>(command.Id, @event);

        Log.Publishers.PublisherCreated(logger, command.Id, command.Name);

        return Results.Created(
            $"/api/admin/publishers/{command.Id}",
            new { id = command.Id, correlationId = session.CorrelationId });
    }

    public static async Task<IResult> Handle(
        UpdatePublisher command,
        IDocumentSession session,
        IHttpContextAccessor httpContextAccessor,
        ILogger logger)
    {
        var streamState = await session.Events.FetchStreamStateAsync(command.Id);
        if (streamState is null)
        {
            Log.Publishers.PublisherNotFound(logger, command.Id);
            return Results.NotFound();
        }

        var context = httpContextAccessor.HttpContext!;
        var currentETag = ETagHelper.GenerateETag(streamState.Version);
        if (!string.IsNullOrEmpty(command.ETag) &&
            !ETagHelper.CheckIfMatch(context, currentETag))
        {
            Log.Publishers.ETagMismatch(logger, command.Id, currentETag, command.ETag);
            return ETagHelper.PreconditionFailed();
        }

        var aggregate = await session.Events.AggregateStreamAsync<PublisherAggregate>(command.Id);
        if (aggregate is null)
        {
            Log.Publishers.PublisherNotFound(logger, command.Id);
            return Results.NotFound();
        }

        Log.Publishers.PublisherUpdating(logger, command.Id, command.Name, streamState.Version);

        var @event = aggregate.UpdateEvent(command.Name);
        _ = session.Events.Append(command.Id, @event);

        Log.Publishers.PublisherUpdated(logger, command.Id);

        var newStreamState = await session.Events.FetchStreamStateAsync(command.Id);
        var newETag = ETagHelper.GenerateETag(newStreamState!.Version);
        ETagHelper.AddETagHeader(context, newETag);

        return Results.NoContent();
    }

    public static async Task<IResult> Handle(
        SoftDeletePublisher command,
        IDocumentSession session,
        IHttpContextAccessor httpContextAccessor,
        ILogger logger)
    {
        Log.Publishers.PublisherSoftDeleting(logger, command.Id);

        var streamState = await session.Events.FetchStreamStateAsync(command.Id);
        if (streamState is null)
        {
            Log.Publishers.PublisherNotFound(logger, command.Id);
            return Results.NotFound();
        }

        var context = httpContextAccessor.HttpContext!;
        var currentETag = ETagHelper.GenerateETag(streamState.Version);
        if (!string.IsNullOrEmpty(command.ETag) &&
            !ETagHelper.CheckIfMatch(context, currentETag))
        {
            return ETagHelper.PreconditionFailed();
        }

        var aggregate = await session.Events.AggregateStreamAsync<PublisherAggregate>(command.Id);
        if (aggregate is null)
        {
            Log.Publishers.PublisherNotFound(logger, command.Id);
            return Results.NotFound();
        }

        var @event = aggregate.SoftDeleteEvent();
        _ = session.Events.Append(command.Id, @event);

        Log.Publishers.PublisherSoftDeleted(logger, command.Id);

        var newStreamState = await session.Events.FetchStreamStateAsync(command.Id);
        var newETag = ETagHelper.GenerateETag(newStreamState!.Version);
        ETagHelper.AddETagHeader(context, newETag);

        return Results.NoContent();
    }

    public static async Task<IResult> Handle(
        RestorePublisher command,
        IDocumentSession session,
        IHttpContextAccessor httpContextAccessor,
        ILogger logger)
    {
        Log.Publishers.PublisherRestoring(logger, command.Id);

        var streamState = await session.Events.FetchStreamStateAsync(command.Id);
        if (streamState is null)
        {
            Log.Publishers.PublisherNotFound(logger, command.Id);
            return Results.NotFound();
        }

        var context = httpContextAccessor.HttpContext!;
        var currentETag = ETagHelper.GenerateETag(streamState.Version);
        if (!string.IsNullOrEmpty(command.ETag) &&
            !ETagHelper.CheckIfMatch(context, currentETag))
        {
            return ETagHelper.PreconditionFailed();
        }

        var aggregate = await session.Events.AggregateStreamAsync<PublisherAggregate>(command.Id);
        if (aggregate is null)
        {
            Log.Publishers.PublisherNotFound(logger, command.Id);
            return Results.NotFound();
        }

        var @event = aggregate.RestoreEvent();
        _ = session.Events.Append(command.Id, @event);

        Log.Publishers.PublisherRestored(logger, command.Id);

        var newStreamState = await session.Events.FetchStreamStateAsync(command.Id);
        var newETag = ETagHelper.GenerateETag(newStreamState!.Version);
        ETagHelper.AddETagHeader(context, newETag);

        return Results.NoContent();
    }
}
