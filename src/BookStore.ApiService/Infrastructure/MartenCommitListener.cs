using BookStore.ApiService.Events;
using BookStore.ApiService.Infrastructure.Logging;
using BookStore.ApiService.Infrastructure.Notifications;
using BookStore.ApiService.Models;
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
        var inserted = commit.Inserted.Count();
        var updated = commit.Updated.Count();
        var deleted = commit.Deleted.Count();

        // High-visibility console log for debugging
        Log.Infrastructure.AfterCommitAsync(_logger, inserted, updated, deleted);

        try
        {
            // Process all document changes with consistent error handling
            await ProcessDocumentChangesAsync(commit.Inserted, ChangeType.Insert, token);
            await ProcessDocumentChangesAsync(commit.Updated, ChangeType.Update, token);
            await ProcessDocumentChangesAsync(commit.Deleted, ChangeType.Delete, token);
        }
        catch (Exception ex)
        {
            Log.Infrastructure.ErrorProcessingProjectionCommit(_logger, ex);
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
                Log.Infrastructure.ProcessingDocumentChange(_logger, changeType.ToString(), doc.GetType().Name);
                await ProcessDocumentChangeAsync(doc, changeType, token);
            }
            catch (Exception ex)
            {
                Log.Infrastructure.ErrorProcessingDocumentChange(_logger, ex, changeType.ToString(), doc.GetType().Name);
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
            case UserProfile profile:
                await HandleUserProfileChangeAsync(profile, changeType, token);
                break;
            case ApplicationUser user:
                await HandleUserDocumentChangeAsync(user, changeType, token);
                break;
            case Models.Tenant tenant:
                await HandleTenantChangeAsync(tenant, changeType, token);
                break;
            case BookStatistics stats:
                await HandleBookStatisticsChangeAsync(stats, changeType, token);
                break;
            case CategoryStatistics stats:
                await HandleCategoryStatisticsChangeAsync(stats, changeType, token);
                break;
            case AuthorStatistics stats:
                await HandleAuthorStatisticsChangeAsync(stats, changeType, token);
                break;
            case PublisherStatistics stats:
                await HandlePublisherStatisticsChangeAsync(stats, changeType, token);
                break;

        }
    }

    async Task HandleUserProfileChangeAsync(UserProfile profile, ChangeType _, CancellationToken token)
    {
        Log.Infrastructure.DebugHandleUserChange(_logger, profile.Id, profile.FavoriteBookIds?.Count ?? -1);

        var timestamp = DateTimeOffset.UtcNow;
        IDomainEventNotification notification = new UserUpdatedNotification(Guid.Empty, profile.Id, timestamp, profile.FavoriteBookIds?.Count ?? 0);

        await NotifyAsync("User", notification, token);
    }

    async Task HandleUserDocumentChangeAsync(ApplicationUser user, ChangeType changeType, CancellationToken token)
    {
        Log.Infrastructure.ProcessingDocumentChange(_logger, changeType.ToString(), nameof(ApplicationUser));

        var timestamp = DateTimeOffset.UtcNow;
        IDomainEventNotification notification = new UserUpdatedNotification(Guid.Empty, user.Id, timestamp, user.Roles.Count);

        await NotifyAsync("User", notification, token);
    }

    async Task HandleTenantChangeAsync(Models.Tenant tenant, ChangeType changeType, CancellationToken token)
    {
        Log.Infrastructure.ProcessingDocumentChange(_logger, changeType.ToString(), nameof(Tenant));

        var timestamp = tenant.UpdatedAt ?? tenant.CreatedAt;
        IDomainEventNotification notification = changeType switch
        {
            ChangeType.Insert => new TenantCreatedNotification(Guid.Empty, tenant.Id, tenant.Name, timestamp),
            ChangeType.Update => new TenantUpdatedNotification(Guid.Empty, tenant.Id, tenant.Name, timestamp),
            _ => new TenantUpdatedNotification(Guid.Empty, tenant.Id, tenant.Name, timestamp) // Fallback
        };

        await NotifyAsync("Tenant", notification, token);
    }

    async Task HandleCategoryChangeAsync(CategoryProjection category, ChangeType changeType, CancellationToken token)
    {
        // Check for soft delete status if it's an update
        var effectiveChangeType = DetermineEffectiveChangeType(changeType, category.Deleted);

        await InvalidateCacheTagsAsync(category.Id, CacheTags.CategoryItemPrefix, CacheTags.CategoryList, token);

        var name = category.Names.Values.FirstOrDefault() ?? "Unknown";
        IDomainEventNotification notification = effectiveChangeType switch
        {
            ChangeType.Insert => new CategoryCreatedNotification(Guid.Empty, category.Id, name, category.LastModified),
            ChangeType.Update => new CategoryUpdatedNotification(Guid.Empty, category.Id, category.LastModified),
            ChangeType.Delete => new CategoryDeletedNotification(Guid.Empty, category.Id, category.LastModified),
            _ => throw new ArgumentOutOfRangeException(nameof(effectiveChangeType))
        };

        await NotifyAsync("Category", notification, token);
    }

    async Task HandleBookChangeAsync(BookSearchProjection book, ChangeType changeType, CancellationToken token)
    {
        var effectiveChangeType = DetermineEffectiveChangeType(changeType, book.Deleted);

        await InvalidateCacheTagsAsync(book.Id, CacheTags.BookItemPrefix, CacheTags.BookList, token);

        // Use UtcNow for books as projections don't track LastModified consistently
        var timestamp = DateTimeOffset.UtcNow;
        IDomainEventNotification notification = effectiveChangeType switch
        {
            ChangeType.Insert => new BookCreatedNotification(Guid.Empty, book.Id, book.Title, timestamp),
            ChangeType.Update => new BookUpdatedNotification(Guid.Empty, book.Id, book.Title, timestamp),
            ChangeType.Delete => new BookDeletedNotification(Guid.Empty, book.Id, timestamp),
            _ => throw new ArgumentOutOfRangeException(nameof(effectiveChangeType))
        };

        await NotifyAsync("Book", notification, token);
    }

    async Task HandleAuthorChangeAsync(AuthorProjection author, ChangeType changeType, CancellationToken token)
    {
        var effectiveChangeType = DetermineEffectiveChangeType(changeType, author.Deleted);

        await InvalidateCacheTagsAsync(author.Id, CacheTags.AuthorItemPrefix, CacheTags.AuthorList, token);

        IDomainEventNotification notification = effectiveChangeType switch
        {
            ChangeType.Insert => new AuthorCreatedNotification(Guid.Empty, author.Id, author.Name, author.LastModified),
            ChangeType.Update => new AuthorUpdatedNotification(Guid.Empty, author.Id, author.Name, author.LastModified),
            ChangeType.Delete => new AuthorDeletedNotification(Guid.Empty, author.Id, author.LastModified),
            _ => throw new ArgumentOutOfRangeException(nameof(effectiveChangeType))
        };

        await NotifyAsync("Author", notification, token);
    }

    async Task HandlePublisherChangeAsync(PublisherProjection publisher, ChangeType changeType, CancellationToken token)
    {
        var effectiveChangeType = DetermineEffectiveChangeType(changeType, publisher.Deleted);

        await InvalidateCacheTagsAsync(publisher.Id, CacheTags.PublisherItemPrefix, CacheTags.PublisherList, token);

        IDomainEventNotification notification = effectiveChangeType switch
        {
            ChangeType.Insert => new PublisherCreatedNotification(Guid.Empty, publisher.Id, publisher.Name, publisher.LastModified),
            ChangeType.Update => new PublisherUpdatedNotification(Guid.Empty, publisher.Id, publisher.Name, publisher.LastModified),
            ChangeType.Delete => new PublisherDeletedNotification(Guid.Empty, publisher.Id, publisher.LastModified),
            _ => throw new ArgumentOutOfRangeException(nameof(effectiveChangeType))
        };

        await NotifyAsync("Publisher", notification, token);
    }

    async Task HandleBookStatisticsChangeAsync(BookStatistics stats, ChangeType _, CancellationToken token)
    {
        Log.Infrastructure.ProcessingDocumentChange(_logger, "Update", nameof(BookStatistics));
        await InvalidateCacheTagsAsync(stats.Id, CacheTags.BookItemPrefix, CacheTags.BookList, token);

        // Emit BookUpdated so clients refetch the book (including new stats)
        IDomainEventNotification notification = new BookUpdatedNotification(Guid.Empty, stats.Id, "Statistics Updated", DateTimeOffset.UtcNow);
        await NotifyAsync("Book", notification, token);
    }

    async Task HandleCategoryStatisticsChangeAsync(CategoryStatistics stats, ChangeType _, CancellationToken token)
    {
        Log.Infrastructure.ProcessingDocumentChange(_logger, "Update", nameof(CategoryStatistics));
        await InvalidateCacheTagsAsync(stats.Id, CacheTags.CategoryItemPrefix, CacheTags.CategoryList, token);

        IDomainEventNotification notification = new CategoryUpdatedNotification(Guid.Empty, stats.Id, DateTimeOffset.UtcNow);
        await NotifyAsync("Category", notification, token);
    }

    async Task HandleAuthorStatisticsChangeAsync(AuthorStatistics stats, ChangeType _, CancellationToken token)
    {
        Log.Infrastructure.ProcessingDocumentChange(_logger, "Update", nameof(AuthorStatistics));
        await InvalidateCacheTagsAsync(stats.Id, CacheTags.AuthorItemPrefix, CacheTags.AuthorList, token);

        IDomainEventNotification notification = new AuthorUpdatedNotification(Guid.Empty, stats.Id, "Statistics Updated", DateTimeOffset.UtcNow);
        await NotifyAsync("Author", notification, token);
    }

    async Task HandlePublisherStatisticsChangeAsync(PublisherStatistics stats, ChangeType _, CancellationToken token)
    {
        Log.Infrastructure.ProcessingDocumentChange(_logger, "Update", nameof(PublisherStatistics));
        await InvalidateCacheTagsAsync(stats.Id, CacheTags.PublisherItemPrefix, CacheTags.PublisherList, token);

        IDomainEventNotification notification = new PublisherUpdatedNotification(Guid.Empty, stats.Id, "Statistics Updated", DateTimeOffset.UtcNow);
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
        var itemTag = CacheTags.ForItem(entityPrefix, id);

        // Invalidate both the specific item and the collection it belongs to
        // We call them individually to avoid any potential issues with collection expression overloads in preview packages
        await _cache.RemoveByTagAsync(itemTag, token);
        await _cache.RemoveByTagAsync(collectionTag, token);

        Log.Infrastructure.CacheInvalidated(_logger, itemTag, collectionTag);
    }

    async Task NotifyAsync(string entityType, IDomainEventNotification notification, CancellationToken token)
    {
        Log.Infrastructure.SendingNotification(_logger, notification.GetType().Name, entityType);
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
