# IAsyncEnumerable\<T\> — Async Streams

## What are async streams?

Async streams let you produce and consume sequences of data asynchronously — yielding each element as it becomes available, without buffering the whole collection first. Think of it as `IEnumerable<T>` + `async`/`await`.

Use async streams when:
- You fetch data page-by-page from an API or database
- You read lines from a network stream or large file
- You produce results incrementally (e.g., streaming AI responses, live sensor data)

---

## Producing async streams — async iterators

An async iterator method returns `IAsyncEnumerable<T>` and uses both `async` and `yield return`:

```csharp
public async IAsyncEnumerable<Product> GetProductsPagedAsync(
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    int page = 0;
    bool hasMore = true;

    while (hasMore)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var batch = await _db.Products
            .Skip(page * PageSize)
            .Take(PageSize)
            .ToListAsync(cancellationToken);

        foreach (var product in batch)
            yield return product;

        hasMore = batch.Count == PageSize;
        page++;
    }
}
```

Key points:
- **`[EnumeratorCancellation]`** on the `CancellationToken` parameter wires up cancellation properly — the compiler propagates the token passed to `GetAsyncEnumerator()` into the iterator body.
- Do NOT put `cancellationToken` before `[EnumeratorCancellation]` in the parameter list — it must be the last parameter.
- You can `yield return` inside `await foreach` or inside loops, and the caller receives each item as it's produced.

---

## Consuming async streams — await foreach

```csharp
await foreach (var product in GetProductsPagedAsync(cancellationToken))
{
    await _indexer.IndexAsync(product, cancellationToken);
}
```

`await foreach` compiles to a `try/finally` that calls `DisposeAsync()` on the enumerator, so resources are always cleaned up.

---

## Passing CancellationToken to the consumer

Two equivalent patterns:

```csharp
// Pattern 1: pass token directly to the producing method (preferred when you control the API)
await foreach (var item in GetItemsAsync(cancellationToken)) { ... }

// Pattern 2: WithCancellation extension — use when you have an IAsyncEnumerable<T>
// from a source that doesn't accept a token, or when you need to override
await foreach (var item in source.WithCancellation(cancellationToken)) { ... }
```

---

## ConfigureAwait on async streams

Disable context capture (for library code) with the `ConfigureAwait` extension on `IAsyncEnumerable<T>`:

```csharp
await foreach (var item in GetItemsAsync().ConfigureAwait(false))
{
    ProcessWithoutContext(item);
}
```

You can combine both:

```csharp
await foreach (var item in GetItemsAsync()
    .WithCancellation(cancellationToken)
    .ConfigureAwait(false))
{
    Process(item);
}
```

---

## Returning ValueTask inside async iterators

The runtime internally uses `ValueTask` in `IAsyncEnumerator<T>.MoveNextAsync()` for efficiency. You don't need to use `ValueTask` yourself in the iterator body — use regular `await` expressions.

---

## LINQ over async streams

In .NET 9+ `System.Linq.AsyncEnumerable` provides LINQ operators over `IAsyncEnumerable<T>`:

```csharp
// Requires System.Linq.AsyncEnumerable (in-box from .NET 9)
var count = await GetProductsAsync().CountAsync(cancellationToken);

var expensive = await GetProductsAsync()
    .Where(p => p.Price > 100)
    .ToListAsync(cancellationToken);
```

For earlier .NET versions, use the `System.Linq.Async` NuGet package.

---

## IAsyncDisposable

Resources produced by async streams are cleaned up via `IAsyncDisposable`. When using `await foreach`, disposal is automatic. If you call `GetAsyncEnumerator()` manually, always dispose:

```csharp
var enumerator = GetItemsAsync().GetAsyncEnumerator(cancellationToken);
try
{
    while (await enumerator.MoveNextAsync())
        Process(enumerator.Current);
}
finally
{
    await enumerator.DisposeAsync();
}
```

Prefer `await foreach` — it handles this for you.

---

## Common mistakes

| Mistake | Fix |
|---|---|
| Omitting `[EnumeratorCancellation]` on token param | Add attribute so compiler wires up `WithCancellation` properly |
| Buffering entire stream with `ToListAsync()` before iterating | Stream instead: `await foreach (var x in stream)` |
| Using `Select(async x => ...)` without awaiting results | Use `await foreach` + explicit processing, or `Task.WhenAll` on a page |
| Not disposing the enumerator when calling `GetAsyncEnumerator()` manually | Use `await using` or `await foreach` |
