# Caching Guide

This guide explains how to configure and use caching in the BookStore API, specifically focusing on the hybrid caching strategy integrated with Aspire and localization.

## Overview

The BookStore API uses **Hybrid Caching** (`HybridCache`), enriched by **Aspire** for seamless distributed cache orchestration.

**Components:**
- **L1 Cache (In-Memory)**: Local, fast access.
- **L2 Cache (Distributed)**: Redis, orchestrated by Aspire.
- **Stampede Protection**: Built-in to coalescing requests.
- **Localization Awareness**: Automatically scopes cache keys to the user's culture.

## Configuration

### Aspire Orchestration

The caching infrastructure is automatically wired up by Aspire.

1.  **AppHost**: Declares the Redis resource.
    ```csharp
    var cache = builder.AddRedis("cache");
    builder.AddProject<Projects.BookStore_ApiService>("apiservice")
           .WithReference(cache);
    ```

2.  **Service Defaults**: Redis configuration is injected via service discovery. The API service adds the distributed cache:
    ```csharp
    builder.Services.AddRedisDistributedCache("cache");
    builder.Services.AddHybridCache();
    ```

## Localized Caching

**Challenge**: Content (e.g., book descriptions) changes based on the user's language (`Accept-Language`). If you cache "book-123" without considering culture, a Portuguese user might receive English content cached by a previous request.

**Solution**: Use the `GetOrCreateLocalizedAsync` extension method.

### Usage Pattern

Instead of `GetOrCreateAsync`, use `GetOrCreateLocalizedAsync`. This method automatically appends the current UI culture (e.g., `|en-US`, `|pt-BR`) to the cache key.

```csharp
public class BookService(HybridCache cache)
{
    public async Task<Book?> GetBookAsync(string id, CancellationToken token = default)
    {
        // Key becomes "book-{id}|{culture}" automatically
        return await cache.GetOrCreateLocalizedAsync(
            key: $"book-{id}",
            factory: async cancel => await RetrieveBookFromDatabaseAsync(id, cancel),
            token: token
        );
    }
}
```

### Invalidation

When invalidating localized content, ensure you remove the localized entry or use tags.

**Remove specific localized entry**:
```csharp
// Removes "book-{id}|{current_culture}"
await cache.RemoveLocalizedAsync($"book-{id}");
```

**Remove by Tag (Recommended)**:
Tags are culture-agnostic. Tagging all variations of a book allows you to clear all languages at once.

```csharp
await cache.GetOrCreateLocalizedAsync(
    $"book-{id}",
    factory,
    tags: [$"book:{id}"] 
);

// Clears English, Portuguese, Spanish, etc. for this book
await cache.RemoveByTagAsync($"book:{id}");
```

## Best Practices

1.  **Use Tags for Entities**: Always tag cache entries with the entity ID (e.g., `book:123`). This makes invalidation much easier than tracking every culture-variant key.
2.  **Use Localized Methods**: Prefer `GetOrCreateLocalizedAsync` for any content that *might* be localized, even if it isn't yet.
3.  **Environment Awareness**: Aspire handles the connection strings. In development, it spins up a Redis container. In production, it points to your managed Redis instance.
