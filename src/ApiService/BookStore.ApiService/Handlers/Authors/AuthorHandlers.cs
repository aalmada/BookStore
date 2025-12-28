using BookStore.ApiService.Aggregates;
using BookStore.ApiService.Commands;
using BookStore.ApiService.Infrastructure;
using Marten;

namespace BookStore.ApiService.Handlers.Authors;

public static class AuthorHandlers
{
    public static IResult Handle(CreateAuthor command, IDocumentSession session)
    {
        var @event = AuthorAggregate.Create(
            command.Id,
            command.Name,
            command.Biography);

        _ = session.Events.StartStream<AuthorAggregate>(command.Id, @event);

        return Results.Created(
            $"/api/admin/authors/{command.Id}",
            new { id = command.Id, correlationId = session.CorrelationId });
    }

    public static async Task<IResult> Handle(
        UpdateAuthor command,
        IDocumentSession session,
        HttpContext context)
    {
        var streamState = await session.Events.FetchStreamStateAsync(command.Id);
        if (streamState == null)
        {
            return Results.NotFound();
        }

        var currentETag = ETagHelper.GenerateETag(streamState.Version);
        if (!string.IsNullOrEmpty(command.ETag) &&
            !ETagHelper.CheckIfMatch(context, currentETag))
        {
            return ETagHelper.PreconditionFailed();
        }

        var aggregate = await session.Events.AggregateStreamAsync<AuthorAggregate>(command.Id);
        if (aggregate == null)
        {
            return Results.NotFound();
        }

        var @event = aggregate.Update(command.Name, command.Biography);
        _ = session.Events.Append(command.Id, @event);

        var newStreamState = await session.Events.FetchStreamStateAsync(command.Id);
        var newETag = ETagHelper.GenerateETag(newStreamState!.Version);
        ETagHelper.AddETagHeader(context, newETag);

        return Results.NoContent();
    }

    public static async Task<IResult> Handle(
        SoftDeleteAuthor command,
        IDocumentSession session,
        HttpContext context)
    {
        var streamState = await session.Events.FetchStreamStateAsync(command.Id);
        if (streamState == null)
        {
            return Results.NotFound();
        }

        var currentETag = ETagHelper.GenerateETag(streamState.Version);
        if (!string.IsNullOrEmpty(command.ETag) &&
            !ETagHelper.CheckIfMatch(context, currentETag))
        {
            return ETagHelper.PreconditionFailed();
        }

        var aggregate = await session.Events.AggregateStreamAsync<AuthorAggregate>(command.Id);
        if (aggregate == null)
        {
            return Results.NotFound();
        }

        var @event = aggregate.SoftDelete();
        _ = session.Events.Append(command.Id, @event);

        var newStreamState = await session.Events.FetchStreamStateAsync(command.Id);
        var newETag = ETagHelper.GenerateETag(newStreamState!.Version);
        ETagHelper.AddETagHeader(context, newETag);

        return Results.NoContent();
    }

    public static async Task<IResult> Handle(
        RestoreAuthor command,
        IDocumentSession session,
        HttpContext context)
    {
        var streamState = await session.Events.FetchStreamStateAsync(command.Id);
        if (streamState == null)
        {
            return Results.NotFound();
        }

        var currentETag = ETagHelper.GenerateETag(streamState.Version);
        if (!string.IsNullOrEmpty(command.ETag) &&
            !ETagHelper.CheckIfMatch(context, currentETag))
        {
            return ETagHelper.PreconditionFailed();
        }

        var aggregate = await session.Events.AggregateStreamAsync<AuthorAggregate>(command.Id);
        if (aggregate == null)
        {
            return Results.NotFound();
        }

        var @event = aggregate.Restore();
        _ = session.Events.Append(command.Id, @event);

        var newStreamState = await session.Events.FetchStreamStateAsync(command.Id);
        var newETag = ETagHelper.GenerateETag(newStreamState!.Version);
        ETagHelper.AddETagHeader(context, newETag);

        return Results.NoContent();
    }
}
