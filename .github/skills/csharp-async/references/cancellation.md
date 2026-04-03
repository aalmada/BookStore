# CancellationToken — Patterns and API Design

## Why CancellationToken matters

`CancellationToken` is the standard .NET mechanism for cooperative cancellation. When a long-running operation (I/O, database query, HTTP request, computation) is no longer needed — because the user navigated away, a timeout fired, or the host is shutting down — passing a token allows the operation to stop cleanly instead of continuing to consume resources.

---

## API design rules

1. **Accept `CancellationToken` as the last parameter** of any async public method.
2. **Name it `cancellationToken`** (the conventional name; tools and analyzers expect it).
3. **Default it to `default`** so callers that don't care don't have to pass it.
4. **Pass it through to every inner async call** — don't swallow it.

```csharp
// Correct public API signature
public async Task<Order> GetOrderAsync(Guid id, CancellationToken cancellationToken = default)
{
    // Pass token to every awaitable call
    var row = await _db.Orders
        .Where(o => o.Id == id)
        .FirstOrDefaultAsync(cancellationToken);

    return row ?? throw new KeyNotFoundException($"Order {id} not found.");
}
```

---

## Checking cancellation in your own loops

For CPU-bound or long-polling work that doesn't call other cancellable APIs, check the token explicitly:

```csharp
public async Task ProcessAllAsync(IEnumerable<Item> items, CancellationToken cancellationToken = default)
{
    foreach (var item in items)
    {
        // Check at each iteration — throws OperationCanceledException if cancelled
        cancellationToken.ThrowIfCancellationRequested();
        await ProcessItemAsync(item, cancellationToken);
    }
}
```

Alternatives to `ThrowIfCancellationRequested()`:

```csharp
// Register a callback (runs when token is cancelled)
using var reg = cancellationToken.Register(() => _cts.Cancel());

// Manual check without throwing
if (cancellationToken.IsCancellationRequested)
    return;  // or break, or return default, depending on context
```

---

## CancellationTokenSource

`CancellationTokenSource` owns the ability to send the cancellation signal; `CancellationToken` is the read-only view you give to others.

```csharp
using var cts = new CancellationTokenSource();

// Manual cancellation
cts.Cancel();

// Timeout cancellation
using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

// Cancel after delay
cts.CancelAfter(TimeSpan.FromSeconds(10));

// The token to pass around
CancellationToken token = cts.Token;
```

Always `Dispose()` a `CancellationTokenSource` when done — use `using` or `await using`.

---

## Linked tokens

When you have a caller-provided token **and** your own timeout, link them into a single token:

```csharp
public async Task<Data> FetchWithTimeoutAsync(
    Uri uri,
    CancellationToken cancellationToken = default)
{
    // Operation should respect both the caller's token and a 10-second timeout
    using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
        cancellationToken, timeoutCts.Token);

    return await _httpClient.GetFromJsonAsync<Data>(uri, linkedCts.Token);
}
```

The linked token fires when **either** source is cancelled.

---

## Handling OperationCanceledException

When a `CancellationToken` fires, awaited operations throw `OperationCanceledException` (or its subclass `TaskCanceledException`). Handle it appropriately:

```csharp
try
{
    await DoWorkAsync(cancellationToken);
}
catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
{
    // Expected — caller requested cancellation. Log if useful, then re-throw or return.
    _logger.LogInformation("Operation was cancelled.");
    throw;  // re-throw to let the framework/caller know it was cancelled
}
catch (OperationCanceledException)
{
    // Cancelled by a different token (e.g., a timeout we created internally)
    throw new TimeoutException("The operation timed out.", ex);
}
```

The `when (cancellationToken.IsCancellationRequested)` guard distinguishes between "the caller cancelled us" and "something else timed out."

---

## CancellationToken in background services

For `BackgroundService` / `IHostedService`, the host provides a `stoppingToken` that fires on graceful shutdown:

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    while (!stoppingToken.IsCancellationRequested)
    {
        await DoUnitOfWorkAsync(stoppingToken);
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
    }
}
```

`Task.Delay` accepts a token; if the token fires, the delay throws `OperationCanceledException` and the loop exits immediately rather than waiting 5 seconds.

---

## CancellationToken.None vs default

`CancellationToken.None` and `default` are equivalent — both produce a token that is never cancelled. Prefer `default` in method signatures; use `CancellationToken.None` when clarity is important at the call site.
