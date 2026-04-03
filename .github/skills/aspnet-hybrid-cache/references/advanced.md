# HybridCache Advanced Patterns

## Object reuse with [ImmutableObject]

By default, `HybridCache` deserializes a fresh copy for each caller to avoid shared-mutable-state bugs. When your cached type is provably immutable, mark it with `[ImmutableObject(true)]` to allow the cached instance to be returned directly, avoiding allocation on every L1 hit:

```csharp
using System.ComponentModel;

[ImmutableObject(true)]
public sealed record BookDto(Guid Id, string Title, string AuthorName, decimal Price);
```

Requirements:
- The type must be `sealed` (otherwise a mutable subclass could be returned).
- All properties must be truly immutable (init-only, readonly, or no setters).
- `record` types with only positional parameters automatically satisfy this.

Do **not** apply to types with mutable collections, settable properties, or any field that callers might mutate.

## Overlay pattern for user-specific data

When **most** of an entity's data is the same for all users but some fields are user-specific (e.g., "is in my wishlist"), cache the shared portion and merge the user-specific part at response time:

```csharp
// 1. Cache the shared, user-agnostic projection (tagged, shared by all users)
var bookDto = await cache.GetOrCreateLocalizedAsync(
    key: $"book:tenant={tenantId}:id={id}",
    factory: async ct => await session.LoadAsync<BookDto>(id, ct),
    options: new HybridCacheEntryOptions { Expiration = ..., LocalCacheExpiration = ... },
    tags: [CacheTags.ForItem(CacheTags.BookItemPrefix, id)],
    cancellationToken: ct);

// 2. Fetch the user-specific data cheaply (not cached — always fresh, tiny query)
var isWishlisted = await wishlistService.IsWishlistedAsync(userId, id, ct);

// 3. Merge at the HTTP handler level
return TypedResults.Ok(bookDto with { IsWishlisted = isWishlisted });
```

Never include user-specific data in the cache key just to make a cache entry per user. That defeats the sharing benefit and explodes cache size.

## Global default options

Set project-wide defaults in `AddHybridCache()`, then override per call only when different TTLs are needed:

```csharp
services.AddHybridCache(options =>
{
    options.DefaultEntryOptions = new HybridCacheEntryOptions
    {
        Expiration = TimeSpan.FromMinutes(5),
        LocalCacheExpiration = TimeSpan.FromMinutes(2)
    };

    // Reject very large payloads from being cached to avoid OOM pressure
    options.MaximumPayloadBytes = 1024 * 1024; // 1 MB
    options.MaximumKeyLength = 1024;           // default; entries with longer keys bypass cache
});
```

## Key length limits

Keys longer than `MaximumKeyLength` (default 1024 characters) are silently ignored — the factory is called every time with no caching. Avoid including:
- Full serialized request bodies in keys
- Long JWT/correlation IDs
- GUIDs without hyphens being repeated many times

Diagnose with: if your factory is always called even after a warm-up, the key is probably too long.

## Working without Redis (L1-only mode)

When Redis is unavailable (e.g., local dev without Redis, unit tests), `HybridCache` automatically falls back to in-memory only. No code change is needed. The `IDistributedCache` registration simply won't be present and the hybrid cache will operate as if there is no L2.

To test L1-only in integration tests without a real Redis container, omit `AddRedisDistributedCache` in the test host setup. `AddHybridCache()` must still be called.

## Serialization

`HybridCache` uses `System.Text.Json` by default for L2 serialization. Ensure your cached types:
- Are correctly annotated for polymorphism if they are interface types (`[JsonPolymorphic]`, `[JsonDerivedType]`)
- Don't use `JsonIgnore` on properties that need to survive a round-trip through Redis

If you get deserialization errors from Redis on startup after a type change, clear the Redis key namespace for that entity type via `RemoveByTagAsync` or by flushing the Redis database.
