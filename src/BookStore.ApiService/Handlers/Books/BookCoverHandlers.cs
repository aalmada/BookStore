using BookStore.ApiService.Aggregates;
using BookStore.ApiService.Commands;
using BookStore.ApiService.Infrastructure;
using BookStore.ApiService.Infrastructure.Tenant;
using BookStore.ApiService.Services;
using BookStore.Shared.Notifications;
using Marten;
using Microsoft.Extensions.Caching.Hybrid;

namespace BookStore.ApiService.Handlers.Books;

public static class BookCoverHandlers
{
    public static async Task<(IResult, BookCoverUpdatedNotification)> Handle(
        UpdateBookCover command,
        IDocumentStore store, // Changed from IDocumentSession
        BlobStorageService blobStorage,
        HybridCache cache,
        IHttpContextAccessor contextAccessor,
        ITenantContext tenantContext)
    {
        var context = contextAccessor.HttpContext;
        var tenantId = command.TenantId ?? tenantContext.TenantId;

        // Open a session explicitly for the target tenant
        // This ensures checking the Correct Aggregate Stream
        await using var session = store.LightweightSession(tenantId);

        var aggregate = await session.Events.AggregateStreamAsync<BookAggregate>(command.BookId);
        if (aggregate is null)
        {
            return (Results.NotFound(), null!);
        }

        var expectedVersion = ETagHelper.ParseETag(command.ETag);
        
        if (expectedVersion.HasValue && aggregate.Version != expectedVersion.Value)
        {
             return (ETagHelper.PreconditionFailed(), null!);
        }

        using var imageStream = new MemoryStream(command.Content);

        // Upload the cover to blob storage (tenant-isolated)
        _ = await blobStorage.UploadBookCoverAsync(
            command.BookId,
            imageStream,
            command.ContentType,
            tenantId);

        // Determine format from content type
        var format = CoverImageFormatExtensions.FromContentType(command.ContentType);

        // Update aggregate with format enum (URL will be generated dynamically by API endpoints)
        var @event = aggregate.UpdateCoverImage(format);
        
        _ = session.Events.Append(command.BookId, @event.Value);

        await session.SaveChangesAsync();

        // Invalidate cache immediately
        await cache.RemoveByTagAsync([CacheTags.BookList, CacheTags.ForItem(CacheTags.BookItemPrefix, command.BookId)], default);

        // Get new stream state and return new ETag
        var newStreamState = await session.Events.FetchStreamStateAsync(command.BookId);
        var newETag = ETagHelper.GenerateETag(newStreamState!.Version);

        if (context is not null)
        {
            ETagHelper.AddETagHeader(context, newETag);
        }

        // Generate URL dynamically for notification (only if we have an HTTP context)
        // During background seeding, context may be null
        string? coverUrl = null;
        if (context?.Request is not null)
        {
            var baseUrl = $"{context.Request.Scheme}://{context.Request.Host}";
            coverUrl = $"{baseUrl}/api/books/{command.BookId}/cover?tenantId={Uri.EscapeDataString(tenantId)}";
        }

        // Return notification for SignalR
        var notification = new BookCoverUpdatedNotification(
            Guid.Empty,
            aggregate.Id,
            coverUrl);

        return (Results.Ok(new { Format = format.ToString() }), notification);
    }
}
