# Task vs ValueTask — Return type selection and async void rules

## Task and Task\<T\>

`Task` and `Task<T>` are the standard async return types. Use them by default for all public APIs.

```csharp
public async Task DoWorkAsync(CancellationToken cancellationToken = default)
{
    await _repository.SaveAsync(cancellationToken);
}

public async Task<User> GetUserAsync(Guid id, CancellationToken cancellationToken = default)
{
    return await _db.Users.FindAsync(id, cancellationToken)
        ?? throw new KeyNotFoundException($"User {id} not found.");
}
```

`Task` is a **reference type** — every async call allocates a new `Task` object on the heap even if the method completes synchronously. That is perfectly fine for most code.

---

## ValueTask and ValueTask\<T\>

`ValueTask<T>` is a `struct` that avoids the heap allocation on the hot path: when the operation already has a result, no `Task` object is created.

**Use `ValueTask<T>` only when:**
- Profiling shows measurable allocation pressure from `Task<T>` in a hot path, **or**
- The method frequently returns a cached or synchronous result (e.g., caches, pooled objects, network framing layers).

```csharp
// Good: cache hit path is synchronous and hot
public ValueTask<User?> GetCachedUserAsync(Guid id)
{
    if (_cache.TryGetValue(id, out var user))
        return ValueTask.FromResult<User?>(user);       // no allocation
    return new ValueTask<User?>(FetchUserFromDbAsync(id));
}
```

### ValueTask rules (easy to violate)

`ValueTask` must only be awaited **once**. It is not safe to:
- Await the same `ValueTask` multiple times
- Store it and await it later if it might already be complete
- Convert it to a `Task` with `.AsTask()` and then also await the original

If you need multiple observers or need to store the result, call `.AsTask()` **once** immediately and keep the `Task`.

```csharp
// Wrong: awaited twice
ValueTask<int> vt = GetValueAsync();
int a = await vt;   // OK
int b = await vt;   // WRONG — undefined behaviour

// Correct: convert to Task first
Task<int> t = GetValueAsync().AsTask();
int a = await t;
int b = await t;    // fine
```

---

## async void — event handlers only

`async void` breaks every async convention:

| Problem | Detail |
|---|---|
| Unobservable exceptions | Exceptions propagate to `SynchronizationContext.UnhandledException`; the process may crash |
| Untestable | Callers cannot `await` it; no way to know when it finishes |
| Fire-and-forget semantics | Caller never knows if it succeeded |

The only legitimate use is event handlers, where the signature is dictated by the framework:

```csharp
// OK: event handler — signature is fixed by the event
private async void SaveButton_Click(object sender, EventArgs e)
{
    try
    {
        await _service.SaveAsync();
    }
    catch (Exception ex)
    {
        // MUST catch here — exception cannot propagate to caller
        ShowError(ex.Message);
    }
}
```

**Always wrap the body of an `async void` handler in try/catch** — there is no other way to observe exceptions.

### Fire-and-forget alternatives

If you want to launch background work without waiting for it, prefer:

```csharp
// Explicit fire-and-forget via Task.Run or background service
_ = Task.Run(() => BackgroundWorkAsync(CancellationToken.None));

// Or capture and discard with logging
_ = DoBackgroundWorkAsync().ContinueWith(
    t => _logger.LogError(t.Exception, "Background task failed"),
    TaskContinuationOptions.OnlyOnFaulted);
```

Avoid `async void` for this purpose — use explicit discard (`_ =`) with a `Task`-returning method so exceptions are at least observable.

---

## Async suffix naming

Add the `Async` suffix to every async method name **except**:
- Event handlers (signature is mandated)
- Framework override methods (e.g., `OnInitializedAsync`, `ExecuteAsync`)

```csharp
// Correct
public Task<Order> GetOrderAsync(Guid id, CancellationToken ct = default);

// Wrong
public Task<Order> GetOrder(Guid id);   // missing Async suffix
```

---

## Generalized async return types

Any type that exposes a `GetAwaiter()` method (returning an object that implements `ICriticalNotifyCompletion`) can be awaited. The runtime and third-party libraries use this to expose custom awaitables (e.g., `YieldAwaitable`, `ConfiguredValueTaskAwaitable`) but you rarely need to implement this yourself.
