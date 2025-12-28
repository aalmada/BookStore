using BookStore.ApiService.Aggregates;
using BookStore.ApiService.Commands;
using BookStore.ApiService.Infrastructure;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BookStore.ApiService.Handlers.Books;

/// <summary>
/// Wolverine handlers for Book commands
/// Handlers are auto-discovered by Wolverine and provide clean separation of concerns
/// </summary>
public static class BookHandlers
{
    /// <summary>
    /// Handle CreateBook command
    /// Wolverine automatically manages the Marten session and commits the transaction
    /// Returns a notification that will be published to SignalR
    /// </summary>
    public static (IResult, BookStore.ApiService.Events.Notifications.BookCreatedNotification) Handle(CreateBook command, IDocumentSession session)
    {
        var @event = BookAggregate.Create(
            command.Id,
            command.Title,
            command.Isbn,
            command.Description,
            command.PublicationDate,
            command.PublisherId,
            command.AuthorIds,
            command.CategoryIds);
        
        session.Events.StartStream<BookAggregate>(command.Id, @event);
        
        // Create notification for SignalR (will be published as cascading message)
        var notification = new BookStore.ApiService.Events.Notifications.BookCreatedNotification(
            command.Id,
            command.Title,
            DateTimeOffset.UtcNow);
        
        // Wolverine automatically calls SaveChangesAsync and publishes the notification
        return (Results.Created(
            $"/api/admin/books/{command.Id}",
            new { id = command.Id, correlationId = session.CorrelationId }), notification);
    }
    
    /// <summary>
    /// Handle UpdateBook command with ETag validation
    /// </summary>
    public static async Task<IResult> Handle(
        UpdateBook command,
        IDocumentSession session,
        HttpContext context)
    {
        // Get current stream state for ETag validation
        var streamState = await session.Events.FetchStreamStateAsync(command.Id);
        if (streamState == null)
            return Results.NotFound();

        var currentETag = ETagHelper.GenerateETag(streamState.Version);

        // Check If-Match header for optimistic concurrency
        if (!string.IsNullOrEmpty(command.ETag) && 
            !ETagHelper.CheckIfMatch(context, currentETag))
        {
            return ETagHelper.PreconditionFailed();
        }

        var aggregate = await session.Events.AggregateStreamAsync<BookAggregate>(command.Id);
        if (aggregate == null)
            return Results.NotFound();

        var @event = aggregate.Update(
            command.Title,
            command.Isbn,
            command.Description,
            command.PublicationDate,
            command.PublisherId,
            command.AuthorIds,
            command.CategoryIds);
        
        session.Events.Append(command.Id, @event);

        // Get new stream state and return new ETag
        var newStreamState = await session.Events.FetchStreamStateAsync(command.Id);
        var newETag = ETagHelper.GenerateETag(newStreamState!.Version);
        ETagHelper.AddETagHeader(context, newETag);

        return Results.NoContent();
    }
    
    /// <summary>
    /// Handle SoftDeleteBook command with ETag validation
    /// </summary>
    public static async Task<IResult> Handle(
        SoftDeleteBook command,
        IDocumentSession session,
        HttpContext context)
    {
        // Get current stream state for ETag validation
        var streamState = await session.Events.FetchStreamStateAsync(command.Id);
        if (streamState == null)
            return Results.NotFound();

        var currentETag = ETagHelper.GenerateETag(streamState.Version);

        // Check If-Match header for optimistic concurrency
        if (!string.IsNullOrEmpty(command.ETag) && 
            !ETagHelper.CheckIfMatch(context, currentETag))
        {
            return ETagHelper.PreconditionFailed();
        }

        var aggregate = await session.Events.AggregateStreamAsync<BookAggregate>(command.Id);
        if (aggregate == null)
            return Results.NotFound();

        var @event = aggregate.SoftDelete();
        session.Events.Append(command.Id, @event);

        // Get new stream state and return new ETag
        var newStreamState = await session.Events.FetchStreamStateAsync(command.Id);
        var newETag = ETagHelper.GenerateETag(newStreamState!.Version);
        ETagHelper.AddETagHeader(context, newETag);

        return Results.NoContent();
    }
    
    /// <summary>
    /// Handle RestoreBook command with ETag validation
    /// </summary>
    public static async Task<IResult> Handle(
        RestoreBook command,
        IDocumentSession session,
        HttpContext context)
    {
        // Get current stream state for ETag validation
        var streamState = await session.Events.FetchStreamStateAsync(command.Id);
        if (streamState == null)
            return Results.NotFound();

        var currentETag = ETagHelper.GenerateETag(streamState.Version);

        // Check If-Match header for optimistic concurrency
        if (!string.IsNullOrEmpty(command.ETag) && 
            !ETagHelper.CheckIfMatch(context, currentETag))
        {
            return ETagHelper.PreconditionFailed();
        }

        var aggregate = await session.Events.AggregateStreamAsync<BookAggregate>(command.Id);
        if (aggregate == null)
            return Results.NotFound();

        var @event = aggregate.Restore();
        session.Events.Append(command.Id, @event);

        // Get new stream state and return new ETag
        var newStreamState = await session.Events.FetchStreamStateAsync(command.Id);
        var newETag = ETagHelper.GenerateETag(newStreamState!.Version);
        ETagHelper.AddETagHeader(context, newETag);

        return Results.NoContent();
    }
}
