# HybridCache Read Patterns

## GetOrCreateAsync — full signature

```csharp
ValueTask<T> GetOrCreateAsync<T>(
    string key,
    Func<CancellationToken, ValueTask<T>> factory,
    HybridCacheEntryOptions? options = null,
    IEnumerable<string>? tags = null,
    CancellationToken cancellationToken = default);
```

The factory is only called on a cache miss (L1 and L2 both miss). On a hit, the factory is never called, so put **all** expensive or IO-bound work inside it.

## GetOrCreateLocalizedAsync — culture-aware extension

Use whenever the cached value depends on the current UI culture (translations, locale-specific formatting):

```csharp
// src/BookStore.ApiService/Infrastructure/Extensions/HybridCacheExtensions.cs
var result = await cache.GetOrCreateLocalizedAsync(
    key:     $"categories:tenant={tenantContext.TenantId}:search={request.Search}",
    factory: async ct => await BuildCategoryListAsync(ct),
    options: new HybridCacheEntryOptions
    {
        Expiration = TimeSpan.FromMinutes(2),
        LocalCacheExpiration = TimeSpan.FromMinutes(1)
    },
    tags: [CacheTags.CategoryList],
    cancellationToken: ct);
```

The extension automatically appends `|{CultureInfo.CurrentUICulture.Name}` to the key before the cache lookup, producing entries like `"categories:tenant=t1:search=xyz|en-US"`. The tag is unaffected, so `RemoveByTagAsync(CacheTags.CategoryList)` still invalidates all culture variants at once.

## Key composition rules

A cache key must include every dimension that makes entries distinct:

| Dimension | When to include | Example segment |
|-----------|----------------|-----------------|
| Tenant ID | Always (multi-tenant API) | `tenant={tenantContext.TenantId}` |
| Culture | Use `GetOrCreateLocalizedAsync` — appended automatically | `|en-US` appended by extension |
| Entity type | Always | `book`, `category` |
| Entity ID | For single-entity endpoints | `id={id}` |
| Search/filter params | For list/search endpoints | `search={q}:page={p}:size={s}` |
| Sort/order | Only if different sorts are cached separately | `sort={orderBy}` |

Use colon (`:`) as segment separator and equals (`=`) for key-value pairs within a segment. Never include the culture manually when using `GetOrCreateLocalizedAsync`.

### Example: list endpoint key

```csharp
var key = $"categories:tenant={tenantContext.TenantId}:search={request.Search}:page={request.Page}:size={request.PageSize}:sort={request.SortBy}";
```

### Example: single-entity endpoint key

```csharp
var key = $"category:tenant={tenantContext.TenantId}:id={id}";
```

## HybridCacheEntryOptions

```csharp
new HybridCacheEntryOptions
{
    Expiration = TimeSpan.FromMinutes(5),           // L2 (Redis) TTL
    LocalCacheExpiration = TimeSpan.FromMinutes(2)  // L1 (in-memory) TTL
}
```

- `Expiration` controls how long the entry lives in Redis (L2). After this, a factory call goes to your data source.
- `LocalCacheExpiration` controls how long the in-process cache (L1) holds the entry before checking L2. Must be ≤ `Expiration`.
- Set per-call options to override the global defaults from `AddHybridCache(options => ...)`.

### Standard expiration values

| Content | `Expiration` (L2) | `LocalCacheExpiration` (L1) |
|---------|-------------------|-----------------------------|
| Single entity | 5 min | 2 min |
| Search / list results | 2 min | 1 min |
| Reference / config data | 10–30 min | 5 min |

## Tagging reads

Always pass at least one tag for every cached entry so it can be invalidated later:

```csharp
tags: [CacheTags.BookList]                                          // list entries
tags: [CacheTags.ForItem(CacheTags.BookItemPrefix, id)]             // single item
tags: [CacheTags.BookList, CacheTags.ForItem(CacheTags.BookItemPrefix, id)]  // both; uncommon on read
```

See [invalidation.md](invalidation.md) for the `CacheTags` constants pattern. A missing tag means `RemoveByTagAsync` will silently skip the entry; always verify tags are present.
