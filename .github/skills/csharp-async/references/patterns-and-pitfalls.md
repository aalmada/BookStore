# Async Patterns and Pitfalls

## ConfigureAwait — when and why

When a task is awaited, the continuation (code after `await`) is scheduled back to the **captured SynchronizationContext** by default. In a WinForms/WPF app this means the UI thread; in ASP.NET Framework it means the request context.

Add `.ConfigureAwait(false)` to tell the runtime: "I don't need to resume on the original context — any thread pool thread will do."

```csharp
// Library code: doesn't need to return to UI/request context
var data = await _httpClient.GetStringAsync(url).ConfigureAwait(false);
var result = Process(data);  // runs on thread pool thread — fine for library code
return result;
```

### When to use ConfigureAwait(false)

| Scenario | Use ConfigureAwait(false)? |
|---|---|
| Library / infrastructure code | ✅ Yes — avoid context dependency |
| ASP.NET Core application code | ➖ Optional — no SynchronizationContext in ASP.NET Core |
| UI application code (WPF, WinForms, MAUI) that touches UI after await | ❌ No — you need the UI thread |
| UI application code that does NOT touch UI after await | ✅ Yes — frees the UI thread sooner |

**Practical guidance for ASP.NET Core**: there is no `SynchronizationContext` in ASP.NET Core, so `ConfigureAwait(false)` is a no-op for deadlock prevention. However, using it in libraries you write makes those libraries safe to call from any host.

### IAsyncEnumerable and ConfigureAwait

For async streams use the extension method:

```csharp
await foreach (var item in GetItemsAsync().ConfigureAwait(false))
{
    Process(item);
}
```

---

## The Classic Deadlock

Calling `.Result` or `.Wait()` on a `Task` from a thread that owns a `SynchronizationContext` creates a deadlock:

1. Synchronous caller blocks the context thread waiting for the `Task`.
2. The `Task`'s continuation needs the same context thread to resume.
3. Neither can proceed — deadlock.

```csharp
// DEADLOCKS in UI apps and ASP.NET Framework
public string GetData()
{
    return GetDataAsync().Result;   // blocks the context thread
}

private async Task<string> GetDataAsync()
{
    await Task.Delay(100);          // continuation needs the original context — it's blocked!
    return "data";
}
```

**Solutions (choose one):**

```csharp
// Best: make the caller async too
public async Task<string> GetDataAsync() => await GetDataInternalAsync();

// Acceptable in rare cases: run on thread pool to avoid the context
public string GetData()
    => Task.Run(() => GetDataAsync()).GetAwaiter().GetResult();

// GetAwaiter().GetResult() is slightly better than .Result:
// it rethrows the original exception instead of wrapping in AggregateException
```

---

## Async All the Way

The single most important async principle: **once you go async, go async all the way up**. Mixing synchronous blocking and async creates deadlocks and defeats the purpose of async.

```csharp
// Wrong: sync entry point blocks on async
public IActionResult GetBook(Guid id)
{
    var book = _service.GetBookAsync(id).Result;   // potential deadlock
    return Ok(book);
}

// Correct: async all the way
public async Task<IActionResult> GetBook(Guid id)
{
    var book = await _service.GetBookAsync(id);
    return Ok(book);
}
```

---

## Task.WhenAll and Task.WhenAny

Prefer `await`-based combinators over blocking ones:

```csharp
// Wrong: blocks threads
Task.WaitAll(task1, task2);
var first = Task.WaitAny(task1, task2);

// Correct: frees threads while waiting
await Task.WhenAll(task1, task2);
var firstCompleted = await Task.WhenAny(task1, task2);
```

### Exception handling with WhenAll

`await Task.WhenAll(...)` throws **only the first exception** even if multiple tasks failed. To observe all exceptions:

```csharp
var tasks = new[] { task1, task2, task3 };
try
{
    await Task.WhenAll(tasks);
}
catch
{
    // Inspect each task for individual exceptions
    var errors = tasks
        .Where(t => t.IsFaulted)
        .Select(t => t.Exception!.InnerException!)
        .ToList();
    throw new AggregateException(errors);
}
```

---

## Async lambdas in LINQ — use with caution

LINQ uses deferred execution — the lambda runs later, not when you write it. Combining this with async is tricky:

```csharp
// WRONG: Select returns IEnumerable<Task<T>> — tasks are not awaited
var results = items.Select(async item => await ProcessAsync(item)).ToList();
// results is List<Task<T>>, not List<T>!

// Correct option 1: use Task.WhenAll
var tasks = items.Select(item => ProcessAsync(item));
var results = await Task.WhenAll(tasks);

// Correct option 2: sequential processing
var results = new List<Result>();
foreach (var item in items)
    results.Add(await ProcessAsync(item));
```

---

## Exception handling in async methods

```csharp
// Exceptions surface naturally at the await site
try
{
    var result = await GetDataAsync();
}
catch (HttpRequestException ex)
{
    _logger.LogError(ex, "Network error");
}

// Unobserved task exceptions: if you discard a Task, add a continuation
_ = RiskyOperationAsync().ContinueWith(
    t => _logger.LogError(t.Exception, "Unobserved task exception"),
    TaskContinuationOptions.OnlyOnFaulted);
```

---

## async void in event handlers — always catch

When using `async void` (only valid in event handlers), exceptions can't propagate to the caller. Always catch:

```csharp
private async void OnTimerElapsed(object? sender, ElapsedEventArgs e)
{
    try
    {
        await DoWorkAsync();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Timer callback failed");
    }
}
```

---

## await Task.Yield()

`Task.Yield()` forces an async method to yield to the caller immediately and resume on the current SynchronizationContext/thread pool. Use it to:
- Prevent a CPU-bound async method from blocking the caller on the first iteration
- Allow the caller to observe cancellation before a tight loop begins

```csharp
public async IAsyncEnumerable<int> GenerateAsync(
    [EnumeratorCancellation] CancellationToken ct = default)
{
    for (int i = 0; i < 1_000_000; i++)
    {
        ct.ThrowIfCancellationRequested();
        yield return i;
        if (i % 100 == 0)
            await Task.Yield();  // give other work a chance to run
    }
}
```
