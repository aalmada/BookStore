---
name: aspnet-hybrid-cache
description: Use Microsoft.Extensions.Caching.Hybrid (HybridCache) to implement two-level L1/L2 caching in ASP.NET Core with stampede protection, tag-based invalidation, and localization-aware keys. Covers GetOrCreateAsync, RemoveByTagAsync, HybridCacheEntryOptions, the CacheTags constants pattern, tenant/culture key scoping, and automatic invalidation via ProjectionCommitListener. Trigger whenever the user writes, reviews, or asks about caching, HybridCache, cache invalidation, RemoveByTagAsync, cache keys, IMemoryCache or IDistributedCache replacements, Redis caching, cache stampede, or stale data after mutations in .NET or ASP.NET Core — even if they don't mention "HybridCache" by name. Always prefer this skill over guessing; HybridCache's tag invalidation model, key length limits, and immutable-object reuse rules have non-obvious behaviours.
---

# ASP.NET Core HybridCache Skill

`HybridCache` (introduced in .NET 9, GA in .NET 10) replaces both `IMemoryCache` and `IDistributedCache` with a single, unified API that automatically manages two cache tiers and prevents cache stampedes. It is the standard caching API for this project.

## Why HybridCache over IMemoryCache / IDistributedCache?

- **L1 + L2 in one call**: a single `GetOrCreateAsync` checks in-memory first, then Redis, then calls your factory — no manual coordination.
- **Stampede protection**: concurrent requests for the same key coalesce — only one factory call is made; all callers await its result.
- **Tag-based invalidation**: group related entries under string tags and invalidate all of them with one `RemoveByTagAsync` call, regardless of how many language/tenant/page-number variants exist.
- **Works without Redis**: falls back to L1 only with no code changes.

## Quick reference

| Topic | See |
|-------|-----|
| Registration: `AddHybridCache`, Aspire Redis integration | [setup.md](references/setup.md) |
| `GetOrCreateAsync`, `GetOrCreateLocalizedAsync`, key design, `HybridCacheEntryOptions` | [read-patterns.md](references/read-patterns.md) |
| `CacheTags` constants, `RemoveByTagAsync`, `ProjectionCommitListener` wiring | [invalidation.md](references/invalidation.md) |
| Overlay pattern, `[ImmutableObject]` reuse, global options, key-length limits | [advanced.md](references/advanced.md) |

## Core anatomy

```csharp
// Read: returns cached value or calls factory
var result = await cache.GetOrCreateAsync(
    key:     "book:123",
    factory: async ct => await session.LoadAsync<BookDto>(id, ct),
    options: new HybridCacheEntryOptions
    {
        Expiration = TimeSpan.FromMinutes(5),           // L2 (Redis) TTL
        LocalCacheExpiration = TimeSpan.FromMinutes(2)  // L1 (in-memory) TTL
    },
    tags: [CacheTags.ForItem(CacheTags.BookItemPrefix, id), CacheTags.BookList],
    cancellationToken: cancellationToken);

// Invalidate: remove all entries carrying these tags from both L1 and L2
await cache.RemoveByTagAsync([CacheTags.BookList, CacheTags.ForItem(CacheTags.BookItemPrefix, id)], ct);
```

## Tagging strategy

Tags decouple cache keys from invalidation logic. One `RemoveByTagAsync("books")` clears every search/pagination/sort/culture/tenant variant of the books list without knowing those keys.

Two tags per entity are the standard pattern:
- **Item tag**: `"book:123"` — clears every language and tenant variant of a single entity
- **Collection tag**: `"books"` — clears all list/search/pagination results for the entity type

Always use `CacheTags.*` constants; never hardcode tag strings inline.

## Expiration strategy

| Content type | L2 (`Expiration`) | L1 (`LocalCacheExpiration`) |
|---|---|---|
| Single entity (stable) | 5 min | 2 min |
| List / search results (dynamic) | 2 min | 1 min |
| Reference / config data | 10–30 min | 5 min |

L1 should always be ≤ L2 to avoid serving stale in-memory data after the distributed cache has been invalidated.

## Common mistakes

- **Missing tags on `GetOrCreateAsync`** → `RemoveByTagAsync` silently does nothing for that entry; data goes stale.
- **Hardcoded tag strings** → tag mismatch between read and invalidation paths; use `CacheTags.*` constants.
- **Invalidating in the command handler instead of `ProjectionCommitListener`** → if the projection update is delayed (async daemon), the cache is re-populated with old data before the projection is updated.
- **`RemoveByTagAsync` with a single string** → `RemoveByTagAsync(tag)` (single string overload) exists but the collection overload `RemoveByTagAsync([tag1, tag2])` batches two logical invalidations; use whichever is clearer but be consistent.
- **Key too long** → keys over 1 024 characters bypass caching silently; include only discriminating parameters, not full DTOs.
- **Culture in key but not using `GetOrCreateLocalizedAsync`** → the localized extension automatically appends `|{culture}` using a pipe delimiter, keeping the base key clean for tag-based invalidation to still work.
