using BookStore.ApiService.Aggregates;
using BookStore.ApiService.Commands;
using BookStore.ApiService.Events.Notifications;
using BookStore.ApiService.Infrastructure;
using BookStore.ApiService.Services;
using Marten;

namespace BookStore.ApiService.Handlers.Books;

public static class BookCoverHandlers
{
    public static async Task<(IResult, BookCoverUpdatedNotification)> Handle(
        UpdateBookCover command,
        IDocumentSession session,
        BlobStorageService blobStorage,
        HttpContext context)
    {
        // Get current stream state for ETag validation
        var streamState = await session.Events.FetchStreamStateAsync(command.BookId);
        if (streamState == null)
        {
            return (Results.NotFound(), null!);
        }

        var currentETag = ETagHelper.GenerateETag(streamState.Version);

        // Check If-Match header for optimistic concurrency
        if (!string.IsNullOrEmpty(command.ETag) &&
            !ETagHelper.CheckIfMatch(context, currentETag))
        {
            return (ETagHelper.PreconditionFailed(), null!);
        }

        var aggregate = await session.Events.AggregateStreamAsync<BookAggregate>(command.BookId);
        if (aggregate == null)
        {
            return (Results.NotFound(), null!);
        }

        // Upload to blob storage
        var coverUrl = await blobStorage.UploadBookCoverAsync(
            command.BookId,
            command.ImageStream,
            command.ContentType);

        // Update aggregate
        var @event = aggregate.UpdateCoverImage(coverUrl);
        _ = session.Events.Append(command.BookId, @event);

        // Get new stream state and return new ETag
        var newStreamState = await session.Events.FetchStreamStateAsync(command.BookId);
        var newETag = ETagHelper.GenerateETag(newStreamState!.Version);
        ETagHelper.AddETagHeader(context, newETag);

        // Return notification for SignalR
        var notification = new BookCoverUpdatedNotification(
            aggregate.Id,
            coverUrl);

        return (Results.Ok(new { CoverUrl = coverUrl }), notification);
    }
}
