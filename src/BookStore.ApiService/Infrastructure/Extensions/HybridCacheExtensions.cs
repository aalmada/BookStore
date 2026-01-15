using System.Globalization;
using Microsoft.Extensions.Caching.Hybrid;

namespace BookStore.ApiService.Infrastructure.Extensions;

/// <summary>
/// Extension methods for HybridCache to support localization-aware caching
/// </summary>
public static class HybridCacheExtensions
{
    /// <summary>
    /// Gets or creates a cache entry using a key that is automatically scoped to the current UI culture.
    /// </summary>
    /// <typeparam name="TItem">The type of the item in the cache.</typeparam>
    /// <param name="cache">The hybrid cache instance.</param>
    /// <param name="key">The base cache key.</param>
    /// <param name="factory">The factory to create the item if it does not exist.</param>
    /// <param name="options">Optional entry options.</param>
    /// <param name="tags">Optional tags for the entry.</param>
    /// <param name="token">Cancellation token.</param>
    /// <returns>The cached or newly created item.</returns>
    public static ValueTask<TItem> GetOrCreateLocalizedAsync<TItem>(
        this HybridCache cache,
        string key,
        Func<CancellationToken, ValueTask<TItem>> factory,
        HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken token = default)
    {
        var localizedKey = GetLocalizedKey(key);
        return cache.GetOrCreateAsync(localizedKey, factory, options, tags, token);
    }

    /// <summary>
    /// Removes a cache entry using a key that is automatically scoped to the current UI culture.
    /// </summary>
    /// <param name="cache">The hybrid cache instance.</param>
    /// <param name="key">The base cache key.</param>
    /// <param name="token">Cancellation token.</param>
    public static ValueTask RemoveLocalizedAsync(
        this HybridCache cache,
        string key,
        CancellationToken token = default)
    {
        var localizedKey = GetLocalizedKey(key);
        return cache.RemoveAsync(localizedKey, token);
    }

    /// <summary>
    /// Helper to append current culture to the key.
    /// Format: "key|culture" (e.g., "book-123|en-US")
    /// </summary>
    static string GetLocalizedKey(string key)
    {
        var culture = CultureInfo.CurrentUICulture.Name;
        // Use a pipe delimiter - distinct from colon usually used for hierarchy
        return $"{key}|{culture}";
    }
}
