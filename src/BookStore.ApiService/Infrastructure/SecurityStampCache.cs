using Microsoft.Extensions.Caching.Hybrid;

namespace BookStore.ApiService.Infrastructure;

/// <summary>
/// Security stamp cache key/tag helpers used during JWT validation.
/// </summary>
public static class SecurityStampCache
{
    const string CacheKeyPrefix = "auth:security-stamp";

    public static string GetCacheKey(string tenantId, Guid userId)
        => $"{CacheKeyPrefix}:{tenantId}:{userId:D}";

    public static string GetCacheTag(string tenantId, Guid userId)
        => CacheTags.ForSecurityStamp(tenantId, userId);

    public static HybridCacheEntryOptions CreateEntryOptions() => new()
    {
        Expiration = TimeSpan.FromSeconds(30),
        LocalCacheExpiration = TimeSpan.FromSeconds(15)
    };

    public static ValueTask InvalidateAsync(
        HybridCache cache,
        string tenantId,
        Guid userId,
        CancellationToken cancellationToken = default)
        => cache.RemoveByTagAsync([GetCacheTag(tenantId, userId)], cancellationToken);
}
