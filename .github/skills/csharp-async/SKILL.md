---
name: csharp-async
description: Write correct, high-performance async/await C# code using Task, ValueTask, CancellationToken, and IAsyncEnumerable — covering return type selection, ConfigureAwait, deadlock prevention, async streams, and cancellation patterns. Trigger whenever the user writes or reviews async methods, mentions Task, ValueTask, async void, ConfigureAwait, IAsyncEnumerable, await foreach, CancellationToken, deadlock, fire-and-forget, or async in C# — even if they don't name any of these explicitly. Always prefer this skill over guessing; async/await has several subtle failure modes (deadlocks, swallowed exceptions, unnecessary allocations) that are easy to get wrong.
---

# C# Async/Await Skill

`async`/`await` turns asynchronous code into readable, sequential-looking code while keeping threads free. Used correctly it scales beautifully; used incorrectly it deadlocks, swallows exceptions, or allocates more than necessary.

## Return type overview

| Return type | When to use |
|---|---|
| `Task` | Async method with no result |
| `Task<T>` | Async method returning a value |
| `ValueTask` | No-result method that often completes synchronously or returns a cached value |
| `ValueTask<T>` | Value-returning method with the same hot-path concern |
| `async void` | **Only** for event handlers — exceptions are unobservable elsewhere |
| `IAsyncEnumerable<T>` | Async iterators / streaming sequences |

## Reference files

| Topic | File |
|---|---|
| Task vs ValueTask — when and why, async void rules | [references/task-and-valuetask.md](references/task-and-valuetask.md) |
| ConfigureAwait, deadlocks, async-all-the-way, and other common mistakes | [references/patterns-and-pitfalls.md](references/patterns-and-pitfalls.md) |
| CancellationToken — API design, linking tokens, handling OperationCanceledException | [references/cancellation.md](references/cancellation.md) |
| IAsyncEnumerable, async iterators, await foreach, stream cancellation | [references/async-streams.md](references/async-streams.md) |

Load the relevant reference file(s) before writing code. For most everyday async work start with `patterns-and-pitfalls.md`. For designing new async APIs add `task-and-valuetask.md`. For streaming data add `async-streams.md`. For any API that takes a long time or can be interrupted add `cancellation.md`.

## Essential rules at a glance

- **Never** use `.Result` or `.Wait()` on a `Task` from synchronous code that runs in a context with a `SynchronizationContext` (UI apps, ASP.NET classic) — this deadlocks.
- **Never** `async void` outside event handlers — exceptions propagate to `SynchronizationContext.UnhandledException` and can crash the process.
- **Do** propagate `async` all the way up the call stack; mixing sync blocking and async is the primary source of deadlocks.
- **Do** use `ConfigureAwait(false)` in library code (code that doesn't need to return to the original UI/request context).
- **Do** pass `CancellationToken` as the last parameter of any async public API; name it `cancellationToken` and default to `default`.
- **Do** call `cancellationToken.ThrowIfCancellationRequested()` or pass the token to inner awaitable calls regularly in long-running operations.
- **Do** prefer `await Task.WhenAll(...)` over `Task.WaitAll(...)` and `await Task.WhenAny(...)` over `Task.WaitAny(...)`.
- **Do** add the `Async` suffix to async method names except for event handlers and framework-mandated overrides.

## Minimal examples

```csharp
// Correct: async all the way up, CancellationToken, Async suffix
public async Task<Order> GetOrderAsync(Guid id, CancellationToken cancellationToken = default)
{
    var order = await _db.FindAsync<Order>(id, cancellationToken);
    return order ?? throw new KeyNotFoundException($"Order {id} not found.");
}

// ValueTask: hot path often returns cached/synchronous result
public ValueTask<string?> GetFromCacheAsync(string key)
{
    if (_cache.TryGetValue(key, out var value))
        return ValueTask.FromResult<string?>(value);
    return new ValueTask<string?>(FetchFromSourceAsync(key));
}

// Async void: ONLY for event handlers
private async void Button_Click(object sender, EventArgs e)
{
    await DoSomethingAsync();
}
```

## The async/await mental model

Think of `await` as "pause here, free the thread, and resume when the operation is done." The compiler rewrites your method into a state machine. The *continuation* (code after `await`) runs:
- On the **original SynchronizationContext** (UI thread, ASP.NET request context) by default — `ConfigureAwait(true)`.
- On **any thread pool thread** when you disable context capture — `ConfigureAwait(false)`.

This distinction is why `ConfigureAwait` exists and why `.Wait()` from a context-owning thread deadlocks.
