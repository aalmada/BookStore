using BookStore.ApiService.Aggregates;
using BookStore.ApiService.Commands.Publishers;
using BookStore.ApiService.Infrastructure;
using Marten;

namespace BookStore.ApiService.Handlers.Publishers;

public static class PublisherHandlers
{
    public static IResult Handle(CreatePublisher command, IDocumentSession session)
    {
        var @event = PublisherAggregate.Create(command.Id, command.Name);
        
        session.Events.StartStream<PublisherAggregate>(command.Id, @event);
        
        return Results.Created(
            $"/api/admin/publishers/{command.Id}",
            new { id = command.Id, correlationId = session.CorrelationId });
    }
    
    public static async Task<IResult> Handle(
        UpdatePublisher command,
        IDocumentSession session,
        HttpContext context)
    {
        var streamState = await session.Events.FetchStreamStateAsync(command.Id);
        if (streamState == null)
            return Results.NotFound();

        var currentETag = ETagHelper.GenerateETag(streamState.Version);
        if (!string.IsNullOrEmpty(command.ETag) && 
            !ETagHelper.CheckIfMatch(context, currentETag))
        {
            return ETagHelper.PreconditionFailed();
        }

        var aggregate = await session.Events.AggregateStreamAsync<PublisherAggregate>(command.Id);
        if (aggregate == null)
            return Results.NotFound();

        var @event = aggregate.Update(command.Name);
        session.Events.Append(command.Id, @event);

        var newStreamState = await session.Events.FetchStreamStateAsync(command.Id);
        var newETag = ETagHelper.GenerateETag(newStreamState!.Version);
        ETagHelper.AddETagHeader(context, newETag);

        return Results.NoContent();
    }
    
    public static async Task<IResult> Handle(
        SoftDeletePublisher command,
        IDocumentSession session,
        HttpContext context)
    {
        var streamState = await session.Events.FetchStreamStateAsync(command.Id);
        if (streamState == null)
            return Results.NotFound();

        var currentETag = ETagHelper.GenerateETag(streamState.Version);
        if (!string.IsNullOrEmpty(command.ETag) && 
            !ETagHelper.CheckIfMatch(context, currentETag))
        {
            return ETagHelper.PreconditionFailed();
        }

        var aggregate = await session.Events.AggregateStreamAsync<PublisherAggregate>(command.Id);
        if (aggregate == null)
            return Results.NotFound();

        var @event = aggregate.SoftDelete();
        session.Events.Append(command.Id, @event);

        var newStreamState = await session.Events.FetchStreamStateAsync(command.Id);
        var newETag = ETagHelper.GenerateETag(newStreamState!.Version);
        ETagHelper.AddETagHeader(context, newETag);

        return Results.NoContent();
    }
    
    public static async Task<IResult> Handle(
        RestorePublisher command,
        IDocumentSession session,
        HttpContext context)
    {
        var streamState = await session.Events.FetchStreamStateAsync(command.Id);
        if (streamState == null)
            return Results.NotFound();

        var currentETag = ETagHelper.GenerateETag(streamState.Version);
        if (!string.IsNullOrEmpty(command.ETag) && 
            !ETagHelper.CheckIfMatch(context, currentETag))
        {
            return ETagHelper.PreconditionFailed();
        }

        var aggregate = await session.Events.AggregateStreamAsync<PublisherAggregate>(command.Id);
        if (aggregate == null)
            return Results.NotFound();

        var @event = aggregate.Restore();
        session.Events.Append(command.Id, @event);

        var newStreamState = await session.Events.FetchStreamStateAsync(command.Id);
        var newETag = ETagHelper.GenerateETag(newStreamState!.Version);
        ETagHelper.AddETagHeader(context, newETag);

        return Results.NoContent();
    }
}
