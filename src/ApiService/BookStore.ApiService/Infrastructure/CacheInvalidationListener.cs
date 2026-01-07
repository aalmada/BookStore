using Marten;
using Marten.Services;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace BookStore.ApiService.Infrastructure;

/// <summary>
/// Listens to Marten document changes and invalidates related cache entries.
/// This ensures cache is cleared AFTER projections are updated.
/// </summary>
public class CacheInvalidationListener : IDocumentSessionListener
{
    readonly HybridCache _cache;
    readonly ILogger<CacheInvalidationListener> _logger;

    public CacheInvalidationListener(HybridCache cache, ILogger<CacheInvalidationListener> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public Task BeforeSaveChangesAsync(IDocumentSession session, CancellationToken token)
        // No action needed before save
        => Task.CompletedTask;

    public Task AfterCommitAsync(IDocumentSession session, IChangeSet commit, CancellationToken token)
        // Invalidate cache for updated and inserted projections
        => InvalidateCacheAsync(commit, token);

    public void DocumentAddedForStorage(object id, object document)
    {
        // No action needed
    }

    public void DocumentLoaded(object id, object document)
    {
        // No action needed
    }

    async Task InvalidateCacheAsync(IChangeSet commit, CancellationToken token)
    {
        // Invalidate cache for updated projections (both item and list caches)
        foreach (var change in commit.Updated)
        {
            await InvalidateProjectionCache(change, invalidateItem: true, token);
        }

        // Invalidate list caches for inserted projections (no item cache exists yet)
        foreach (var change in commit.Inserted)
        {
            await InvalidateProjectionCache(change, invalidateItem: false, token);
        }
    }

    async Task InvalidateProjectionCache(object projection, bool invalidateItem, CancellationToken token)
    {
        var (itemTag, listTag) = projection switch
        {
            // Add new projection types here to ensure proper cache invalidation
            Projections.CategoryProjection category => ($"category:{category.Id}", "categories"),
            Projections.AuthorProjection author => ($"author:{author.Id}", "authors"),
            Projections.BookSearchProjection book => ($"book:{book.Id}", "books"),
            Projections.PublisherProjection publisher => ($"publisher:{publisher.Id}", "publishers"),
            _ => (null, null)
        };

        if (itemTag == null || listTag == null)
        {
            // Warn if cache invalidation is not implemented for this projection type
            Logging.Log.Infrastructure.CacheInvalidationNotImplemented(_logger, projection.GetType().Name);
            return;
        }

        // Invalidate specific item cache (all language variants)
        if (invalidateItem)
        {
            await _cache.RemoveByTagAsync(itemTag, token);
        }

        // Invalidate list caches
        await _cache.RemoveByTagAsync(listTag, token);
    }
}
