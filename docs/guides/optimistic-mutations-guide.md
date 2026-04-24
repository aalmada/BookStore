# Optimistic Mutations Guide

This guide describes the BookStore frontend pattern for optimistic UI updates using `ReactiveQuery<T>.MutateAsync(...)` and `CatalogService`.

## Why this Pattern Exists

Optimistic UI gives immediate feedback while a mutation is in-flight. Without a reusable pattern, every component must re-implement:

1. snapshot current state
2. apply optimistic state
3. call API
4. rollback on failure
5. handle success/error notifications

BookStore now centralizes this flow so components stay thin and consistent.

## Architecture

The optimistic mutation stack has three layers:

1. **ReactiveQuery** (`src/BookStore.Web/Services/ReactiveQuery.cs`)
   - Owns query state (`Data`, loading, errors)
   - Provides `MutateAsync` for generic optimistic lifecycle
2. **CatalogService** (`src/BookStore.Web/Services/CatalogService.cs`)
   - Encodes domain-specific optimistic transforms (favorite, rating, soft delete, restore)
   - Calls Refit clients and displays snackbar feedback
3. **Components** (`.razor` pages)
   - Trigger mutations by calling service methods with the active query
   - Do not implement rollback logic inline

## Core Primitive: `ReactiveQuery<T>.MutateAsync`

`MutateAsync` implements the reusable optimistic lifecycle:

1. Store a snapshot of current `Data`
2. Apply optimistic transform
3. Execute server mutation
4. On failure, restore snapshot and rethrow

```csharp
await query.MutateAsync(
    applyOptimistic: current => current with { IsFavorite = true },
    mutation: ct => _booksClient.AddBookToFavoritesAsync(book.Id, cancellationToken: ct),
    cancellationToken: cancellationToken);
```

## Service Pattern

`CatalogService` should own mutation behavior and keep component code minimal.

### Detail query (`ReactiveQuery<BookDto?>`)

```csharp
public async Task SoftDeleteBookAsync(BookDto book, ReactiveQuery<BookDto?> query, CancellationToken cancellationToken = default)
{
    try
    {
        await query.MutateAsync(
            current => current == null ? null : current with { IsDeleted = true },
            ct => _booksClient.SoftDeleteBookAsync(book.Id, book.ETag, ct),
            cancellationToken);

        _ = _snackbar.Add("Book deleted", Severity.Success);
    }
    catch (Exception ex)
    {
        Log.BookDeleteFailed(_logger, book.Id, ex);
        _ = _snackbar.Add($"Failed to delete book: {ex.Message}", Severity.Error);
    }
}
```

### List query (`ReactiveQuery<PagedListDto<BookDto>>`)

For list updates, mutate only the matching row and return a new list DTO.

```csharp
await query.MutateAsync(
    currentList =>
    {
        var items = currentList.Items.ToList();
        var index = items.FindIndex(b => b.Id == book.Id);
        if (index != -1)
        {
            items[index] = items[index] with
            {
                IsFavorite = !originalState,
                LikeCount = originalState ? items[index].LikeCount - 1 : items[index].LikeCount + 1
            };
        }

        return new PagedListDto<BookDto>(items, currentList.PageNumber, currentList.PageSize, currentList.TotalItemCount);
    },
    mutation: ct => _booksClient.AddBookToFavoritesAsync(book.Id, cancellationToken: ct),
    cancellationToken: cancellationToken);
```

## Component Usage

Components should pass the active query object and avoid custom rollback lambdas.

```csharp
if (bookQuery == null) return;
await CatalogService.ToggleFavoriteAsync(book, bookQuery, _cts.Token);
```

## SSE and Eventual Consistency

Book projections are asynchronous. After delete/restore, a direct immediate `LoadAsync()` can read stale projection data.

Preferred behavior:

1. keep optimistic local state from `MutateAsync`
2. let SSE-triggered invalidation refresh in background when projection catches up

Avoid forcing immediate reload after delete/restore unless there is a strong consistency requirement.

## Do / Don't

```text
✅ Use ReactiveQuery.MutateAsync for optimistic lifecycle
✅ Keep mutation transforms in CatalogService (or domain service), not in components
✅ Keep components as orchestration-only for UI actions
✅ Let SSE invalidation reconcile eventual consistency

❌ Reintroduce setOptimistic/setRollback lambdas in .razor files
❌ Reintroduce separate optimistic ghost-state cache for this flow
❌ Immediately force LoadAsync() after async-projection mutations by default
```

## Migration Checklist

When converting older optimistic code:

1. Replace inline `MutateData` + try/catch rollback with `query.MutateAsync`
2. Move transform logic into service methods
3. Update component calls to pass query (`CatalogService.XxxAsync(book, bookQuery, ct)`)
4. Keep snackbar/logging in service catch blocks
5. Validate with:

```bash
dotnet build BookStore.slnx
dotnet test tests/BookStore.Web.Tests/
dotnet format BookStore.slnx --verify-no-changes
```
