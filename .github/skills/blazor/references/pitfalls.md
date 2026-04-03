# Common Pitfalls in Blazor Components

## 1. Missing IDisposable → Memory Leak and Exceptions

**Problem**: Subscribing to `EventsService.OnNotificationReceived` but not unsubscribing means the handler continues to fire after the component is navigated away from, eventually throwing `ObjectDisposedException` or running code on a disposed circuit.

```razor
@* ❌ Wrong — no cleanup *@
protected override void OnInitialized()
{
    EventsService.OnNotificationReceived += HandleNotification;
}
```

```razor
@* ✅ Correct *@
@implements IDisposable

protected override void OnInitialized()
{
    EventsService.OnNotificationReceived += HandleNotification;
}

public void Dispose()
{
    EventsService.OnNotificationReceived -= HandleNotification;
    _cts.Cancel();
    _cts.Dispose();
    _query?.Dispose();
}
```

---

## 2. Missing QueryInvalidationService Mapping → UI Never Refreshes

**Problem**: You add a new entity (`Widget`), add `BookStoreEventsService` notifications on the backend, but forget to register the query key mapping in `QueryInvalidationService`. SSE events arrive at the component but `ShouldInvalidate` returns `false`.

```csharp
// ❌ Wrong — "Widgets" key is unknown
_query = new ReactiveQuery<IReadOnlyList<WidgetDto>>(
    queryKeys: ["Widgets"], ...);
// ShouldInvalidate always returns false for Widget notifications
```

```csharp
// ✅ Correct — also update QueryInvalidationService.ShouldInvalidate:
return notification switch
{
    WidgetCreatedNotification or WidgetUpdatedNotification or WidgetDeletedNotification
        => keys.Contains("Widgets"),
    // ... other cases
};
```

---

## 3. StateHasChanged on the Wrong Thread

**Problem**: SSE `HandleNotification` is called from a background thread. Calling `StateHasChanged()` directly throws or silently does nothing.

```csharp
// ❌ Wrong — called from SSE callback thread
private void HandleNotification(IDomainEventNotification n)
{
    StateHasChanged();  // InvalidOperationException: not the Blazor synchronization context
}
```

```csharp
// ✅ Correct — marshal to Blazor thread
private async void HandleNotification(IDomainEventNotification n)
{
    await InvokeAsync(StateHasChanged);
    // or for async work:
    await InvokeAsync(async () => { await _table.ReloadServerData(); StateHasChanged(); });
}
```

---

## 4. Calling HttpClient Directly

**Problem**: Bypasses `TenantHeaderHandler`, `AuthorizationMessageHandler`, and Polly resilience. Results in missing tenant headers, missing auth tokens, and no retry logic.

```csharp
// ❌ Wrong
@inject HttpClient Http
var books = await Http.GetFromJsonAsync<List<BookDto>>("/api/books");
```

```csharp
// ✅ Correct — always use the Refit client interfaces
@inject IBooksClient BooksClient
var result = await BooksClient.GetBooksAsync(page, pageSize, ct);
```

---

## 5. Business Logic Inside .razor Files

**Problem**: Razor files mix presentation and domain logic, making the code untestable and hard to maintain.

```csharp
// ❌ Wrong — pricing logic leaking into the component
private decimal CalculateDiscountedPrice(BookDto book)
{
    if (book.Sale?.Percentage > 0)
        return book.Price * (1 - book.Sale.Percentage / 100m);
    return book.Price;
}
```

```csharp
// ✅ Correct — move to a service in Services/ or a domain helper
// The component just renders:
<MudText>@PricingService.GetDiscountedPrice(book)</MudText>
```

---

## 6. Missing StartListening() Call

**Problem**: SSE never connects even though `HandleNotification` is subscribed.

```csharp
// ❌ Wrong — forgot to connect the SSE stream
protected override void OnInitialized()
{
    EventsService.OnNotificationReceived += HandleNotification;
    // EventsService never starts the SSE HTTP connection
}
```

```csharp
// ✅ Correct
protected override void OnInitialized()
{
    EventsService.StartListening();   // ← must be called first
    EventsService.OnNotificationReceived += HandleNotification;
}
```

---

## 7. async void Without Error Handling

**Problem**: Exceptions in `async void` methods are unobserved and can crash the Blazor circuit silently.

```csharp
// ❌ Risky — unhandled exceptions crash the circuit
private async void HandleNotification(IDomainEventNotification n)
{
    await InvokeAsync(async () => await _table.ReloadServerData());
}
```

```csharp
// ✅ Safer
private async void HandleNotification(IDomainEventNotification n)
{
    try
    {
        await InvokeAsync(async () => await _table.ReloadServerData());
    }
    catch (Exception ex)
    {
        Logger.LogWarning(ex, "SSE notification handling failed");
    }
}
```

---

## 8. Render Mode on Dialog Components

**Problem**: Adding `@rendermode InteractiveServer` to a dialog component causes it to have a different render context than the parent, breaking parameter passing and cascading values.

```razor
@* ❌ Wrong — dialog should inherit render mode *@
@rendermode InteractiveServer
```

```razor
@* ✅ No rendermode directive on dialog components *@
@* They inherit InteractiveServer from the parent page *@
```

---

## 9. Blocking the Blazor Thread in OnInitializedAsync

**Problem**: Calling `Thread.Sleep` or doing CPU-bound work synchronously blocks UI rendering.

```csharp
// ❌ Wrong
protected override async Task OnInitializedAsync()
{
    Thread.Sleep(500);  // blocks the Blazor thread
}
```

```csharp
// ✅ Correct — use async/await for all waits
// Use WaitForConditionAsync for tests; avoid waits in production code entirely
protected override async Task OnInitializedAsync()
{
    await LoadDataAsync();
}
```
