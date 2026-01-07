# Caching Guide

This guide explains how to configure and use caching in the BookStore API, specifically focusing on the hybrid caching strategy integrated with Aspire and localization.

## Overview

The BookStore API uses **Hybrid Caching** (`HybridCache`), enriched by **Aspire** for seamless distributed cache orchestration.

**Components:**
- **L1 Cache (In-Memory)**: Local, fast access per instance
- **L2 Cache (Distributed)**: Redis, orchestrated by Aspire, shared across instances
- **Stampede Protection**: Built-in request coalescing prevents cache stampedes
- **Localization Awareness**: Automatically scopes cache keys to the user's culture

## Configuration

### Aspire Orchestration

The caching infrastructure is automatically wired up by Aspire.

1.  **AppHost**: Declares the Redis resource.
    ```csharp
    var cache = builder.AddRedis("cache");
    builder.AddProject<Projects.BookStore_ApiService>("apiservice")
           .WithReference(cache);
    ```

2.  **API Service**: Redis configuration is injected via Aspire service discovery.
    ```csharp
    // In Program.cs
    builder.AddRedisDistributedCache("cache");  // L2 distributed cache
    
    // In ApplicationServicesExtensions.cs
    services.AddHybridCache();  // Registers HybridCache service
    ```

## Usage in Endpoints

### Localized Content (Categories, Authors, Books)

For content that varies by language, use `GetOrCreateLocalizedAsync`. This method automatically appends the current UI culture to the cache key.

**Example from CategoryEndpoints**:
```csharp
static async Task<Ok<CategoryDto>> GetCategory(
    Guid id,
    [FromServices] IDocumentStore store,
    [FromServices] IOptions<LocalizationOptions> localizationOptions,
    [FromServices] HybridCache cache,
    CancellationToken cancellationToken)
{
    var culture = CultureInfo.CurrentCulture.Name;
    var defaultCulture = localizationOptions.Value.DefaultCulture;

    var response = await cache.GetOrCreateLocalizedAsync(
        $"category:{id}",
        async cancel =>
        {
            await using var session = store.QuerySession();
            var category = await session.LoadAsync<CategoryProjection>(id, cancel);
            if (category == null)
                return (CategoryDto?)null;

            var localizedName = LocalizationHelper.GetLocalizedValue(
                category.Names, culture, defaultCulture, "Unknown");

            return new CategoryDto(category.Id, localizedName);
        },
        options: new HybridCacheEntryOptions
        {
            Expiration = TimeSpan.FromMinutes(5),      // L2 (Redis) expiration
            LocalCacheExpiration = TimeSpan.FromMinutes(2)  // L1 (in-memory) expiration
        },
        tags: [$"category:{id}"],
        token: cancellationToken);

    return response is null ? TypedResults.NotFound() : TypedResults.Ok(response);
}
```

**Cache key format**: `category:123|en-US`, `category:123|pt-PT`, etc.

### Non-Localized Content (Publishers)

For content that doesn't vary by language, use `GetOrCreateAsync`.

**Example from PublisherEndpoints**:
```csharp
var response = await cache.GetOrCreateAsync(
    $"publisher:{id}",
    async cancel =>
    {
        var publisher = await session.LoadAsync<PublisherProjection>(id, cancel);
        return publisher == null ? null : new PublisherDto(publisher.Id, publisher.Name);
    },
    options: new HybridCacheEntryOptions
    {
        Expiration = TimeSpan.FromMinutes(5),
        LocalCacheExpiration = TimeSpan.FromMinutes(2)
    },
    tags: [$"publisher:{id}"],
    cancellationToken: cancellationToken);
```

### Complex Queries with Parameters

For search or list endpoints, include all query parameters in the cache key.

**Example from BookEndpoints**:
```csharp
var cacheKey = $"books:search={request.Search}:author={request.AuthorId}:category={request.CategoryId}:publisher={request.PublisherId}:page={paging.Page}:size={paging.PageSize}:sort={normalizedSortBy}:{normalizedSortOrder}";

var response = await cache.GetOrCreateLocalizedAsync(
    cacheKey,
    async cancel => { /* query logic */ },
    options: new HybridCacheEntryOptions
    {
        Expiration = TimeSpan.FromMinutes(2),  // Shorter for dynamic content
        LocalCacheExpiration = TimeSpan.FromMinutes(1)
    },
    tags: ["books"],
    token: cancellationToken);
```

## Cache Invalidation

### Tag-Based Invalidation (Recommended)

Tags are culture-agnostic and allow clearing all language variants at once.

```csharp
// When creating/updating a book, tag it
await cache.GetOrCreateLocalizedAsync(
    $"book:{id}",
    factory,
    tags: [$"book:{id}"]  // Tag with entity ID
);

// Invalidate all language variants
await cache.RemoveByTagAsync($"book:{id}");  // Clears en-US, pt-PT, etc.
```

### Direct Removal

Remove specific localized entries:

```csharp
// Removes "category:{id}|{current_culture}"
await cache.RemoveLocalizedAsync($"category:{id}");
```

## Cache Expiration Strategy

| Content Type | L2 (Redis) | L1 (In-Memory) | Rationale |
|-------------|-----------|----------------|-----------|
| Single entities (book, author, category) | 5 min | 2 min | Relatively stable, infrequent updates |
| Search results | 2 min | 1 min | Dynamic, parameter-dependent |
| Lists (paginated) | 5 min | 2 min | Moderate stability |

## Best Practices

1.  **Use Tags for Entities**: Always tag cache entries with the entity ID (e.g., `book:123`). This makes invalidation much easier than tracking every culture-variant key.
2.  **Use Localized Methods**: Prefer `GetOrCreateLocalizedAsync` for any content that *might* be localized, even if it isn't yet.
3.  **Include All Parameters**: For search/filter endpoints, include all parameters in the cache key to avoid serving stale results.
4.  **Set Appropriate Expiration**: Balance freshness vs. performance. Dynamic content should have shorter TTLs.
5.  **Environment Awareness**: Aspire handles the connection strings. In development, it spins up a Redis container. In production, it points to your managed Redis instance.

## Architecture Benefits

✅ **Two-Tier Caching**: L1 (fast, local) + L2 (shared, distributed)  
✅ **Stampede Protection**: Built-in request coalescing  
✅ **Localization-Aware**: Automatic culture-based cache keys  
✅ **Tag-Based Invalidation**: Clear all language variants at once  
✅ **Aspire Integration**: Automatic Redis orchestration and service discovery
