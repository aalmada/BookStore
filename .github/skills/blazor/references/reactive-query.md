# ReactiveQuery\<T\> — SSE-Driven Data Loading

`ReactiveQuery<T>` is the BookStore equivalent of React's `useQuery`. It manages:
- Data fetching with version tracking (prevents stale-data overwrites from concurrent requests)
- Loading/fetching/error state
- Automatic re-fetch when SSE events match the query's `queryKeys`
- Thread-safe disposal

**File**: `src/BookStore.Web/Services/ReactiveQuery.cs`

---

## Constructor

```csharp
_query = new ReactiveQuery<PagedListDto<BookDto>>(
    queryFn:             ct => BooksClient.GetBooksAsync(pageIndex, pageSize, ct),
    eventsService:       EventsService,        // injected BookStoreEventsService
    invalidationService: InvalidationService,  // injected QueryInvalidationService
    queryKeys:           ["Books", "Authors"], // any key that should trigger a refetch
    onStateChanged:      () => InvokeAsync(StateHasChanged),  // marshal to Blazor thread
    logger:              Logger);
```

Subscribe and load:
```csharp
await _query.LoadAsync(cancellationToken: _cts.Token);
```

**ReactiveQuery subscribes to `EventsService.OnNotificationReceived` in its constructor** — do not manually subscribe a second handler for the same keys unless you need extra side-effects (e.g., showing a snackbar).

---

## State Properties

| Property | Type | Description |
|---|---|---|
| `Data` | `T?` | Current data; `null` until first successful load |
| `IsLoading` | `bool` | `true` during first load when `Data` is null |
| `IsFetching` | `bool` | `true` during any fetch (including background refresh) |
| `Error` | `string?` | Error message from last failed fetch |
| `IsError` | `bool` | `true` when `Error != null` |

---

## Template Pattern — Three States

Always render all three states: loading skeleton, error, and content. Avoid showing a full spinner when `IsFetching && Data != null` — show a lightweight progress indicator instead so the existing content stays visible:

```razor
@if (_query?.IsLoading == true && _query.Data == null)
{
    @* First-load skeleton — no data to show yet *@
    <MudStack>
        <MudSkeleton SkeletonType="SkeletonType.Rectangle" Height="60px" />
        <MudSkeleton SkeletonType="SkeletonType.Rectangle" Height="60px" />
    </MudStack>
}
else if (_query?.IsError == true)
{
    <MudAlert Severity="Severity.Error">@_query.Error</MudAlert>
}
else
{
    @* Background-fetch indicator — shows while data is refreshing *@
    @if (_query?.IsFetching == true)
    {
        <MudProgressLinear Indeterminate="true" Color="Color.Primary" />
    }

    @foreach (var item in _query!.Data!)
    {
        <WidgetCard Item="item" />
    }
}
```

---

## LoadAsync — Silent vs. Full Refresh

```csharp
// Full refresh: shows loading skeleton if no data yet
await _query.LoadAsync(cancellationToken: _cts.Token);

// Silent refresh: keeps existing data visible, only shows IsFetching
await _query.LoadAsync(silent: true, cancellationToken: _cts.Token);
```

Use `silent: true` for background SSE-triggered refreshes so the UI does not flicker.

---

## Handling Notifications Alongside ReactiveQuery

`ReactiveQuery` handles re-fetching automatically. If you also need to show a snackbar or update unrelated UI state on an SSE event, register a *separate* handler:

```csharp
protected override void OnInitialized()
{
    EventsService.StartListening();
    EventsService.OnNotificationReceived += HandleNotification;
}

private async void HandleNotification(IDomainEventNotification notification)
{
    if (notification is PingNotification) return;
    if (InvalidationService.ShouldInvalidate(notification, ["Widgets"]))
    {
        await InvokeAsync(async () =>
        {
            Snackbar.Add("Data updated.", Severity.Info);
            await _table!.ReloadServerData();   // for MudTable-based pages (no ReactiveQuery)
            StateHasChanged();
        });
    }
}
```

**`async void` risk**: unhandled exceptions in `async void` methods crash the circuit. Wrap in `try/catch` if the body can throw:

```csharp
private async void HandleNotification(IDomainEventNotification notification)
{
    try { /* ... */ }
    catch (Exception ex) { Logger.LogWarning(ex, "Notification handling failed"); }
}
```

---

## Optimistic Data Mutation

For immediate UI updates before the server confirms, use `MutateData`:

```csharp
// After toggling a favorite locally:
_query?.MutateData(data => data with { IsFavorite = !data.IsFavorite });
```

For list-level optimistic operations (book creation), use `OptimisticUpdateService`:

```csharp
// Before calling the API:
OptimisticService.AddOptimisticBook(optimisticPlaceholder);

// After SSE confirms BookCreated:
OptimisticService.ConfirmBook(confirmedId);
```

Render placeholders above the real list:
```razor
@foreach (var pending in OptimisticService.GetOptimisticBooks())
{
    <MudChip T="string" Color="Color.Warning">Saving…</MudChip>
    <BookCard Book="pending.Book" Disabled="true" />
}
```

---

## QueryInvalidationService — Registering New Query Keys

When you add a new domain entity (e.g., `Widget`), you must add a mapping in `QueryInvalidationService.ShouldInvalidate()` so `ReactiveQuery` knows when to refetch:

```csharp
// In QueryInvalidationService.cs:
public bool ShouldInvalidate(IDomainEventNotification notification, IEnumerable<string> queryKeys)
{
    var keys = queryKeys.ToHashSet();
    return notification switch
    {
        WidgetCreatedNotification or WidgetUpdatedNotification or WidgetDeletedNotification
            => keys.Contains("Widgets"),
        // ... existing mappings
        _ => false
    };
}
```

Without this mapping, SSE events arrive at the component but `ShouldInvalidate` returns `false`, so the UI never refreshes.
