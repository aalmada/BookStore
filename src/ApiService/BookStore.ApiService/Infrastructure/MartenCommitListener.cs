using BookStore.ApiService.Events;
using BookStore.ApiService.Infrastructure.Notifications;
using BookStore.ApiService.Projections;
using BookStore.Shared.Notifications;
using Marten;
using Marten.Services;
using Microsoft.Extensions.Caching.Hybrid;

namespace BookStore.ApiService.Infrastructure;

/// <summary>
/// Marten session listener to invalidate HybridCache and trigger SSE notifications when PROJECTIONS are committed.
/// This works for Async Projections because it listens to the DOCUMENT changes committed by the Async Daemon.
/// </summary>
public class ProjectionCommitListener : IDocumentSessionListener, IChangeListener
{
    readonly HybridCache _cache;
    readonly INotificationService _notificationService;
    readonly ILogger<ProjectionCommitListener> _logger;

    public ProjectionCommitListener(
        HybridCache cache,
        INotificationService notificationService,
        ILogger<ProjectionCommitListener> logger)
    {
        _cache = cache;
        _notificationService = notificationService;
        _logger = logger;
    }

    public Task BeforeSaveChangesAsync(IDocumentSession _, CancellationToken __)
        => Task.CompletedTask;

    public Task AfterSaveChangesAsync(IDocumentSession _, CancellationToken __)
        => Task.CompletedTask;

    public Task BeforeCommitAsync(IDocumentSession _, IChangeSet __, CancellationToken ___)
        => Task.CompletedTask;

    public async Task AfterCommitAsync(IDocumentSession _, IChangeSet commit, CancellationToken token)
    {
        // For Async Projections, the 'commit' contains the changes to the Read Models (Documents),
        // not the original Events.
        _logger.LogDebug("AfterCommitAsync called. Inserted: {InsertedCount}, Updated: {UpdatedCount}, Deleted: {DeletedCount}",
            commit.Inserted.Count(), commit.Updated.Count(), commit.Deleted.Count());

        try
        {
            // Process all document changes with consistent error handling
            await ProcessDocumentChangesAsync(commit.Inserted, ChangeType.Insert, token);
            await ProcessDocumentChangesAsync(commit.Updated, ChangeType.Update, token);
            await ProcessDocumentChangesAsync(commit.Deleted, ChangeType.Delete, token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing projection commit");
        }
    }

    public void AfterCommit(IDocumentSession _, IChangeSet __)
    {
        // Sync hook not used
    }

    async Task ProcessDocumentChangesAsync(IEnumerable<object> documents, ChangeType changeType, CancellationToken token)
    {
        foreach (var doc in documents)
        {
            try
            {
                _logger.LogDebug("Processing {ChangeType}: {DocumentType}", changeType, doc.GetType().Name);
                await ProcessDocumentChangeAsync(doc, changeType, token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing {ChangeType} document of type {DocumentType}", changeType, doc.GetType().Name);
            }
        }
    }

    async Task ProcessDocumentChangeAsync(object document, ChangeType changeType, CancellationToken token)
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

    async Task HandleCategoryChangeAsync(CategoryProjection category, ChangeType changeType, CancellationToken token)
    {
        // Check for soft delete status if it's an update
        var effectiveChangeType = DetermineEffectiveChangeType(changeType, category.IsDeleted);

        await InvalidateCacheTagsAsync(category.Id, CacheTags.CategoryItemPrefix, CacheTags.CategoryList, token);

        var name = category.Names.Values.FirstOrDefault() ?? "Unknown";
        IDomainEventNotification notification = effectiveChangeType switch
        {
            ChangeType.Insert => new CategoryCreatedNotification(category.Id, name, category.LastModified),
            ChangeType.Update => new CategoryUpdatedNotification(category.Id, category.LastModified),
            ChangeType.Delete => new CategoryDeletedNotification(category.Id, category.LastModified),
            _ => throw new ArgumentOutOfRangeException(nameof(effectiveChangeType))
        };

        await NotifyAsync("Category", notification, token);
    }

    async Task HandleBookChangeAsync(BookSearchProjection book, ChangeType changeType, CancellationToken token)
    {
        var effectiveChangeType = DetermineEffectiveChangeType(changeType, book.IsDeleted);

        await InvalidateCacheTagsAsync(book.Id, CacheTags.BookItemPrefix, CacheTags.BookList, token);

        // Use UtcNow for books as projections don't track LastModified consistently
        var timestamp = DateTimeOffset.UtcNow;
        IDomainEventNotification notification = effectiveChangeType switch
        {
            ChangeType.Insert => new BookCreatedNotification(book.Id, book.Title, timestamp),
            ChangeType.Update => new BookUpdatedNotification(book.Id, book.Title, timestamp),
            ChangeType.Delete => new BookDeletedNotification(book.Id, timestamp),
            _ => throw new ArgumentOutOfRangeException(nameof(effectiveChangeType))
        };

        await NotifyAsync("Book", notification, token);
    }

    async Task HandleAuthorChangeAsync(AuthorProjection author, ChangeType changeType, CancellationToken token)
    {
        var effectiveChangeType = DetermineEffectiveChangeType(changeType, author.IsDeleted);

        await InvalidateCacheTagsAsync(author.Id, CacheTags.AuthorItemPrefix, CacheTags.AuthorList, token);

        IDomainEventNotification notification = effectiveChangeType switch
        {
            ChangeType.Insert => new AuthorCreatedNotification(author.Id, author.Name, author.LastModified),
            ChangeType.Update => new AuthorUpdatedNotification(author.Id, author.Name, author.LastModified),
            ChangeType.Delete => new AuthorDeletedNotification(author.Id, author.LastModified),
            _ => throw new ArgumentOutOfRangeException(nameof(effectiveChangeType))
        };

        await NotifyAsync("Author", notification, token);
    }

    async Task HandlePublisherChangeAsync(PublisherProjection publisher, ChangeType changeType, CancellationToken token)
    {
        var effectiveChangeType = DetermineEffectiveChangeType(changeType, publisher.IsDeleted);

        await InvalidateCacheTagsAsync(publisher.Id, CacheTags.PublisherItemPrefix, CacheTags.PublisherList, token);

        IDomainEventNotification notification = effectiveChangeType switch
        {
            ChangeType.Insert => new PublisherCreatedNotification(publisher.Id, publisher.Name, publisher.LastModified),
            ChangeType.Update => new PublisherUpdatedNotification(publisher.Id, publisher.Name, publisher.LastModified),
            ChangeType.Delete => new PublisherDeletedNotification(publisher.Id, publisher.LastModified),
            _ => throw new ArgumentOutOfRangeException(nameof(effectiveChangeType))
        };

        await NotifyAsync("Publisher", notification, token);
    }

    static ChangeType DetermineEffectiveChangeType(ChangeType changeType, bool isDeleted)
    {
        // If it's an update but the entity is soft-deleted, treat it as a delete for notifications
        if (changeType == ChangeType.Update && isDeleted)
        {
            return ChangeType.Delete;
        }

        return changeType;
    }

    async Task InvalidateCacheTagsAsync(Guid id, string entityPrefix, string collectionTag, CancellationToken token)
    {
        var itemTag = $"{entityPrefix}:{id}";
        await _cache.RemoveByTagAsync(itemTag, token);
        await _cache.RemoveByTagAsync(collectionTag, token);
        _logger.LogDebug("Invalidated cache {ItemTag} and {ListTag}", itemTag, collectionTag);
    }

    async Task NotifyAsync(string entityType, IDomainEventNotification notification, CancellationToken token)
    {
        _logger.LogInformation("Sending {NotificationType} for {EntityType}", notification.GetType().Name, entityType);
        await _notificationService.NotifyAsync(notification, token);
    }

    enum ChangeType
    {
        Insert,
        Update,
        Delete
    }

    public void BeforeSaveChanges(IDocumentSession _) { }
    public void AfterSaveChanges(IDocumentSession _) { }
    public void DocumentLoaded(object _, object __) { }
    public void DocumentAddedForStorage(object _, object __) { }
}
