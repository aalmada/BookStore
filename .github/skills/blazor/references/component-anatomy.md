# Component Anatomy — Render Mode, DI, Lifecycle, IDisposable

## File Header — The Canonical Order

Every Blazor page or component in BookStore follows this directive order:

```razor
@page "/route"                    ← pages only; omit for non-routed components
@rendermode InteractiveServer     ← pages only; dialogs/shared components inherit
@implements IDisposable           ← whenever you subscribe to events or own CancellationTokenSource

@using BookStore.Shared           ← only if not covered by _Imports.razor
@inject IWidgetsClient WidgetsClient
@inject BookStoreEventsService EventsService
@inject QueryInvalidationService InvalidationService
@inject ISnackbar Snackbar
@inject IDialogService DialogService
@attribute [Authorize(Roles = "Admin")]   ← page-level auth guard
```

**Rule of thumb**: `@rendermode` and `@implements` before `@inject`/`@attribute`. Shared components (e.g., `ThemeSwitcher.razor`, dialog components like `AddBookDialog.razor`) omit `@rendermode` — they inherit from their parent.

---

## Dependency Injection — Two Styles

Both styles appear in the project; the convention is to prefer `@inject` for clarity:

| Style | When to use |
|---|---|
| `@inject IFoo Foo` (directive) | Preferred for services used in the template markup |
| `[Inject] private IBar Bar { get; set; } = default!;` in `@code` | Use when the property is only needed inside `@code` and not referenced in the template |

```razor
@inject IWidgetsClient WidgetsClient    ← used in template: @WidgetsClient (rare, but clear)

@code {
    [Inject] private ILogger<Widgets> Logger { get; set; } = default!;
    [Inject] private QueryInvalidationService InvalidationService { get; set; } = default!;
}
```

---

## Lifecycle Methods

| Method | Use for |
|---|---|
| `OnInitialized()` | Sync subscriptions (SSE event handler registration) |
| `OnInitializedAsync()` | Initial data load, tenant initialization |
| `OnParametersSetAsync()` | React to route parameter or `[Parameter]` changes |
| `OnAfterRenderAsync(firstRender)` | JS interop — only safe after first render |

**Admin pages** typically subscribe to SSE in `OnInitialized()` (sync) and load data in `OnInitializedAsync()`:

```csharp
protected override void OnInitialized()
{
    EventsService.StartListening();
    EventsService.OnNotificationReceived += HandleNotification;
}

protected override async Task OnInitializedAsync()
{
    await LoadDataAsync();
}
```

**Catalog pages** that use `ReactiveQuery<T>` combine both into `OnInitializedAsync()` because `ReactiveQuery` subscribes to SSE in its constructor:

```csharp
protected override async Task OnInitializedAsync()
{
    await TenantService.InitializeAsync();    // tenant-aware pages

    _query = new ReactiveQuery<PagedListDto<BookDto>>(
        queryFn:             ct => FetchBooksAsync(ct),
        eventsService:       EventsService,
        invalidationService: InvalidationService,
        queryKeys:           ["Books", "Authors", "Publishers"],
        onStateChanged:      () => InvokeAsync(StateHasChanged),
        logger:              Logger);

    await _query.LoadAsync(cancellationToken: _cts.Token);
}
```

---

## IDisposable — Mandatory Cleanup Pattern

Any component that:
- Subscribes to `EventsService.OnNotificationReceived`
- Holds a `CancellationTokenSource`
- Subscribes to a service's `OnChange` / `OnBooksChanged` event
- Owns a `ReactiveQuery<T>` or `System.Threading.Timer`

…**must** implement `IDisposable` and clean up in `Dispose()`. Not doing this causes SSE events to fire on a disposed component, leading to `ObjectDisposedException` or stale UI updates.

### The _disposed Guard

Use a `_disposed` flag to make `Dispose()` idempotent:

```csharp
private readonly CancellationTokenSource _cts = new();
private bool _disposed;

public void Dispose()
{
    if (_disposed) return;
    _disposed = true;

    // 1. Cancel any in-flight async work
    _cts.Cancel();
    _cts.Dispose();

    // 2. Dispose ReactiveQuery (unsubscribes its SSE handler)
    _query?.Dispose();

    // 3. Unsubscribe any manually registered events
    EventsService.OnNotificationReceived -= HandleNotification;
    CurrencyService.OnCurrencyChanged    -= HandleCurrencyChanged;
    TenantService.OnChange               -= HandleTenantChanged;
    OptimisticService.OnBooksChanged     -= HandleOptimisticBooksChanged;

    // 4. Dispose any timers
    debounceTimer?.Dispose();
}
```

**Note**: `ReactiveQuery<T>` already unsubscribes its own `HandleNotification` in its `Dispose()`. You still need to unsubscribe any *additional* handlers you registered directly on `EventsService`.

---

## Component Parameters

```razor
@code {
    // Required parameter — no default
    [Parameter, EditorRequired] public Guid BookId { get; set; }

    // Optional parameter with default
    [Parameter] public bool ShowBadge { get; set; } = true;

    // Cascading parameter (e.g., from MudDialog)
    [CascadingParameter] IMudDialogInstance MudDialog { get; set; } = null!;

    // Cascading auth state
    [CascadingParameter] private Task<AuthenticationState> AuthStateTask { get; set; } = default!;
}
```
