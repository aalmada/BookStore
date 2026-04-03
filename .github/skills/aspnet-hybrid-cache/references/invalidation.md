# HybridCache Invalidation

## CacheTags constants

All tag strings live in a single static class. Use constants everywhere — never hardcode tag strings inline. This ensures a read-side tag always matches the write-side invalidation.

```csharp
// src/BookStore.ApiService/Infrastructure/CacheTags.cs
public static class CacheTags
{
    // Item prefixes  (one per entity type)
    public const string BookItemPrefix = "book";
    public const string CategoryItemPrefix = "category";
    public const string AuthorItemPrefix = "author";
    public const string PublisherItemPrefix = "publisher";

    // Collection tags  (one per entity type)
    public const string BookList = "books";
    public const string CategoryList = "categories";
    public const string AuthorList = "authors";
    public const string PublisherList = "publishers";

    // Item tag helper: "prefix:guid"
    public static string ForItem(string prefix, Guid id) => $"{prefix}:{id}";
}
```

When you add a new entity type, add both an item prefix constant and a collection tag constant here.

## Invalidation in mutation handlers

Call `RemoveByTagAsync` **after** the command has been successfully processed. Pass both the item tag and the collection tag so:
1. All single-entity variants (any culture, any tenant) are invalidated.
2. All list/search/pagination variants are invalidated.

```csharp
// Inside a Wolverine handler after applying the command
await cache.RemoveByTagAsync(
    [CacheTags.CategoryList, CacheTags.ForItem(CacheTags.CategoryItemPrefix, command.Id)],
    cancellationToken);
```

The `RemoveByTagAsync` overload that takes `IEnumerable<string>` performs both invalidations in one call. Use `default` when an explicit `CancellationToken` is not available (e.g., inside `MartenCommitListener`).

## Automatic invalidation via ProjectionCommitListener

For Marten-projected read models, cache invalidation happens inside `MartenCommitListener` **after** the projection has been committed to the database. This ensures the cache is only invalidated when new data is ready, avoiding a thundering herd of requests hitting stale projections.

```csharp
// src/BookStore.ApiService/Infrastructure/MartenCommitListener.cs  (simplified)
private async Task InvalidateCacheTagsAsync(
    Guid id, string entityPrefix, string collectionTag, CancellationToken ct)
{
    await _cache.RemoveByTagAsync(CacheTags.ForItem(entityPrefix, id), ct);
    await _cache.RemoveByTagAsync(collectionTag, ct);
}

private async Task HandleCategoryChangeAsync(Guid id, ChangeType change, CancellationToken ct)
{
    await InvalidateCacheTagsAsync(id, CacheTags.CategoryItemPrefix, CacheTags.CategoryList, ct);
    // Optionally: fire SSE notification here too
}
```

The listener calls the two `RemoveByTagAsync` overloads separately (single string) rather than the array overload — both approaches are valid.

## Adding a new entity type — checklist

1. Add item prefix and collection tag constants to `CacheTags`.
2. Tag `GetOrCreateAsync` / `GetOrCreateLocalizedAsync` calls with the correct tags.
3. In mutation Wolverine handlers, call `RemoveByTagAsync([...list, ...item])` after success.
4. In `MartenCommitListener.ProcessDocumentChangeAsync`, add a `case` for the new projection type and call `HandleXxxChangeAsync`.
5. Add a `HandleXxxChangeAsync` method that calls `InvalidateCacheTagsAsync` with the new constants.

## RemoveByTagAsync overloads

```csharp
// Invalidate a single tag (both L1 and L2)
await cache.RemoveByTagAsync("books", ct);

// Invalidate multiple tags in one call
await cache.RemoveByTagAsync(["books", "book:abc123"], ct);
```

Both forms invalidate from both L1 and L2. Invalidation is logical: existing entries are marked invalid and will be a miss on next read, but they still consume memory until their TTL expires naturally.

## Soft-delete handling

When an entity is soft-deleted (update with `IsDeleted = true`), the listener's `DetermineEffectiveChangeType` maps it to `ChangeType.Delete` so the same `HandleXxxChangeAsync` method is called and the item+list tags are both invalidated. No special handling is needed in the handler.
