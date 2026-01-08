using BookStore.ApiService.Events;
using BookStore.ApiService.Projections;
using Marten;
using Marten.Services;
using Microsoft.Extensions.Caching.Hybrid;
using BookStore.ApiService.Infrastructure.Notifications;
using BookStore.Shared.Notifications;

namespace BookStore.ApiService.Infrastructure;

/// <summary>
/// Marten session listener to invalidate HybridCache and trigger SSE notifications when PROJECTIONS are committed.
/// This works for Async Projections because it listens to the DOCUMENT changes committed by the Async Daemon.
/// </summary>
public class ProjectionCommitListener : IDocumentSessionListener, IChangeListener
{
    private readonly HybridCache _cache;
    private readonly INotificationService _notificationService;
    private readonly ILogger<ProjectionCommitListener> _logger;

    public ProjectionCommitListener(
        HybridCache cache, 
        INotificationService notificationService,
        ILogger<ProjectionCommitListener> logger)
    {
        _cache = cache;
        _notificationService = notificationService;
        _logger = logger;
    }

    public Task BeforeSaveChangesAsync(IDocumentSession session, CancellationToken token)
        => Task.CompletedTask;

    public Task AfterSaveChangesAsync(IDocumentSession session, CancellationToken token)
        => Task.CompletedTask;

    public Task BeforeCommitAsync(IDocumentSession session, IChangeSet commit, CancellationToken token)
        => Task.CompletedTask;

    public async Task AfterCommitAsync(IDocumentSession session, IChangeSet commit, CancellationToken token)
    {
        // For Async Projections, the 'commit' contains the changes to the Read Models (Documents),
        // not the original Events.
        _logger.LogDebug("AfterCommitAsync called. Inserted: {InsertedCount}, Updated: {UpdatedCount}, Deleted: {DeletedCount}",
            commit.Inserted.Count(), commit.Updated.Count(), commit.Deleted.Count());

        try
        {
            // Process Inserted Documents
            foreach (var doc in commit.Inserted)
            {
                _logger.LogDebug("Processing Insert: {DocumentType}", doc.GetType().Name);
                await ProcessDocumentChangeAsync(doc, ChangeType.Insert, token);
            }

            foreach (var doc in commit.Updated)
            {
                try
                {
                    switch (doc)
                    {
                        case CategoryProjection category:
                            await HandleCategoryChangeAsync(category, ChangeType.Update, token);
                            break;
                        case BookSearchProjection book:
                            await HandleBookChangeAsync(book, ChangeType.Update, token);
                            break;
                        case AuthorProjection author:
                            await HandleAuthorChangeAsync(author, ChangeType.Update, token);
                            break;
                        case PublisherProjection publisher:
                            await HandlePublisherChangeAsync(publisher, ChangeType.Update, token);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing updated document of type {DocumentType}", doc.GetType().Name);
                }
            }

            // Process Deleted Documents
            foreach (var doc in commit.Deleted)
            {
                _logger.LogDebug("Processing Delete: {DocumentType}", doc.GetType().Name);
                await ProcessDocumentChangeAsync(doc, ChangeType.Delete, token);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing projection commit");
        }
    }

    public void AfterCommit(IDocumentSession session, IChangeSet commit)
    {
        // Sync hook not used
    }

    private async Task ProcessDocumentChangeAsync(object document, ChangeType changeType, CancellationToken token)
    {
        switch (document)
        {
            case CategoryProjection category:
                await HandleCategoryChangeAsync(category, changeType, token);
                break;
            case BookSearchProjection book:
                await HandleBookChangeAsync(book, changeType, token);
                break;
            case AuthorProjection author:
                await HandleAuthorChangeAsync(author, changeType, token);
                break;
            case PublisherProjection publisher:
                await HandlePublisherChangeAsync(publisher, changeType, token);
                break;
        }
    }

    private async Task HandleCategoryChangeAsync(CategoryProjection category, ChangeType changeType, CancellationToken token)
    {
        // Check for soft delete status if it's an update
        if (changeType == ChangeType.Update)
        {
            if (category.IsDeleted) changeType = ChangeType.Delete;
            // If it was deleted but now IsDeleted is false, we treat it as an Update (or Insert, but cache invalidation is same)
        }

        await InvalidateCacheTagsAsync(category.Id, "category", "categories", token);

        var name = category.Names.Values.FirstOrDefault() ?? "Unknown";
        IDomainEventNotification notification = changeType switch
        {
            ChangeType.Insert => new CategoryCreatedNotification(category.Id, name, category.LastModified),
            ChangeType.Update => new CategoryUpdatedNotification(category.Id, category.LastModified),
            ChangeType.Delete => new CategoryDeletedNotification(category.Id, category.LastModified),
            _ => throw new ArgumentOutOfRangeException(nameof(changeType))
        };

        await NotifyAsync("Category", notification, token);
    }

    private async Task HandleBookChangeAsync(BookSearchProjection book, ChangeType changeType, CancellationToken token)
    {
        if (changeType == ChangeType.Update && book.IsDeleted)
        {
            changeType = ChangeType.Delete;
        }

        await InvalidateCacheTagsAsync(book.Id, "book", "books", token);

        // For books, we might want to know *which* book updated, but for list invalidation, tag is enough.
        // For notification, we use the title.
        IDomainEventNotification notification = changeType switch
        {
            ChangeType.Insert => new BookCreatedNotification(book.Id, book.Title, DateTimeOffset.UtcNow), // Projections don't always track CreatedAt, so using UtcNow or LastModified
            ChangeType.Update => new BookUpdatedNotification(book.Id, book.Title, DateTimeOffset.UtcNow),
            ChangeType.Delete => new BookDeletedNotification(book.Id, DateTimeOffset.UtcNow),
            _ => throw new ArgumentOutOfRangeException(nameof(changeType))
        };

        await NotifyAsync("Book", notification, token);
    }

    private async Task HandleAuthorChangeAsync(AuthorProjection author, ChangeType changeType, CancellationToken token)
    {
         if (changeType == ChangeType.Update && author.IsDeleted)
        {
            changeType = ChangeType.Delete;
        }

        await InvalidateCacheTagsAsync(author.Id, "author", "authors", token);

        IDomainEventNotification notification = changeType switch
        {
            ChangeType.Insert => new AuthorCreatedNotification(author.Id, author.Name, author.LastModified),
            ChangeType.Update => new AuthorUpdatedNotification(author.Id, author.Name, author.LastModified),
            ChangeType.Delete => new AuthorDeletedNotification(author.Id, author.LastModified),
            _ => throw new ArgumentOutOfRangeException(nameof(changeType))
        };

        await NotifyAsync("Author", notification, token);
    }

    private async Task HandlePublisherChangeAsync(PublisherProjection publisher, ChangeType changeType, CancellationToken token)
    {
         if (changeType == ChangeType.Update && publisher.IsDeleted)
        {
            changeType = ChangeType.Delete;
        }

        await InvalidateCacheTagsAsync(publisher.Id, "publisher", "publishers", token);

        IDomainEventNotification notification = changeType switch
        {
            ChangeType.Insert => new PublisherCreatedNotification(publisher.Id, publisher.Name, publisher.LastModified),
            ChangeType.Update => new PublisherUpdatedNotification(publisher.Id, publisher.Name, publisher.LastModified),
            ChangeType.Delete => new PublisherDeletedNotification(publisher.Id, publisher.LastModified),
            _ => throw new ArgumentOutOfRangeException(nameof(changeType))
        };

        await NotifyAsync("Publisher", notification, token);
    }

    private async Task InvalidateCacheTagsAsync(Guid id, string entityPrefix, string collectionTag, CancellationToken token)
    {
        var itemTag = $"{entityPrefix}:{id}";
        await _cache.RemoveByTagAsync(itemTag, token);
        await _cache.RemoveByTagAsync(collectionTag, token);
        _logger.LogDebug("Invalidated cache {ItemTag} and {ListTag}", itemTag, collectionTag);
    }

    private async Task NotifyAsync(string entityType, IDomainEventNotification notification, CancellationToken token)
    {
        _logger.LogInformation("Sending {NotificationType} for {EntityType}", notification.GetType().Name, entityType);
        await _notificationService.NotifyAsync(notification, token);
    }

    private enum ChangeType
    {
        Insert,
        Update,
        Delete
    }

    public void BeforeSaveChanges(IDocumentSession session) { }
    public void AfterSaveChanges(IDocumentSession session) { }
    public void DocumentLoaded(object id, object document) { }
    public void DocumentAddedForStorage(object id, object document) { }
}
