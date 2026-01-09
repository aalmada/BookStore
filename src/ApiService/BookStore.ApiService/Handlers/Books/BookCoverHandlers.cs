using BookStore.ApiService.Aggregates;
using BookStore.ApiService.Commands;
using BookStore.ApiService.Infrastructure;
using BookStore.ApiService.Services;
using BookStore.Shared.Notifications;
using Marten;

namespace BookStore.ApiService.Handlers.Books;

public static class BookCoverHandlers
{
    public static async Task<(IResult, BookCoverUpdatedNotification)> Handle(
        UpdateBookCover command,
        IDocumentSession session,
        BlobStorageService blobStorage,
        IHttpContextAccessor contextAccessor)
    {
        var context = contextAccessor.HttpContext;

        // Get current stream state for ETag validation
        var streamState = await session.Events.FetchStreamStateAsync(command.BookId);
        if (streamState is null)
        {
            return (Results.NotFound(), null!);
        }

        var currentETag = ETagHelper.GenerateETag(streamState.Version);

        // Check If-Match header for optimistic concurrency
        if (context is not null && !string.IsNullOrEmpty(command.ETag) &&
            !ETagHelper.CheckIfMatch(context, currentETag))
        {
            return (ETagHelper.PreconditionFailed(), null!);
        }

        var aggregate = await session.Events.AggregateStreamAsync<BookAggregate>(command.BookId);
        if (aggregate is null)
        {
            return (Results.NotFound(), null!);
        }

        using var imageStream = new MemoryStream(command.Content);
        // Upload to blob storage
        var coverUrl = await blobStorage.UploadBookCoverAsync(
            command.BookId,
            imageStream,
            command.ContentType);

        // Update aggregate
        var @event = aggregate.UpdateCoverImage(coverUrl);
        _ = session.Events.Append(command.BookId, @event);

        // Get new stream state and return new ETag
        var newStreamState = await session.Events.FetchStreamStateAsync(command.BookId);
        var newETag = ETagHelper.GenerateETag(newStreamState!.Version);
        
        if (context is not null)
        {
            ETagHelper.AddETagHeader(context, newETag);
        }

        // Return notification for SignalR
        var notification = new BookCoverUpdatedNotification(
            aggregate.Id,
            coverUrl);

        return (Results.Ok(new { CoverUrl = coverUrl }), notification);
    }
}
