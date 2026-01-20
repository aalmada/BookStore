using Microsoft.Extensions.Caching.Distributed;

namespace BookStore.ApiService.Infrastructure.Tenant;

/// <summary>
/// Cached wrapper around ITenantStore to reduce database queries.
/// Uses distributed cache (Redis) to store tenant validation results.
/// </summary>
public class CachedTenantStore(ITenantStore inner, IDistributedCache cache) : ITenantStore
{
    const string CacheKeyPrefix = "tenant:valid:";
    static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public async Task<bool> IsValidTenantAsync(string tenantId)
    {
        var cacheKey = $"{CacheKeyPrefix}{tenantId}";

        // Try to get from cache
        var cached = await cache.GetStringAsync(cacheKey);
        if (cached != null)
        {
            return bool.Parse(cached);
        }

        // Cache miss - query the inner store
        var isValid = await inner.IsValidTenantAsync(tenantId);

        // Store in cache with sliding expiration (keeps frequently accessed tenants warm)
        await cache.SetStringAsync(
            cacheKey,
            isValid.ToString(),
            new DistributedCacheEntryOptions
            {
                SlidingExpiration = CacheDuration
            });

        return isValid;
    }

    public async Task<IEnumerable<string>> GetAllTenantsAsync()
        // Don't cache the full list - it's rarely called and changes frequently
        => await inner.GetAllTenantsAsync();

    /// <summary>
    /// Invalidates the cache for a specific tenant.
    /// Call this when a tenant is created, updated, or deleted.
    /// </summary>
    public async Task InvalidateCacheAsync(string tenantId)
    {
        var cacheKey = $"{CacheKeyPrefix}{tenantId}";
        await cache.RemoveAsync(cacheKey);
    }
}
