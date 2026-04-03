---
name: blazor
description: Write, review, and fix Blazor Server components in the BookStore project — covering render modes (InteractiveServer), lifecycle with IDisposable cleanup, DI via @inject/[Inject], ReactiveQuery<T> for SSE-driven data loading, MudBlazor forms/dialogs/tables, tenant-aware services, and AuthorizeView guards. Trigger whenever the user writes, reviews, or asks about .razor files, adding a page or component, ReactiveQuery, MudForm/MudTable/MudDialog, real-time UI updates from SSE, tenant-aware components, optimistic updates in the frontend, authorization guards in pages, or BookStore.Web — even if they don't say "Blazor" explicitly.
---

# Blazor Components — BookStore Conventions

BookStore's Blazor Server frontend is built around a few key abstractions: `ReactiveQuery<T>` for reactive data fetching, `BookStoreEventsService` for SSE subscriptions, and MudBlazor for all UI components. Getting these patterns right avoids the most common failure modes: missing `IDisposable` cleanups, forgetting SSE event propagation, and bypassing the Refit client layer.

## Quick Reference

| Topic | Read this file |
|---|---|
| Component skeleton, render mode, DI, lifecycle, IDisposable | `references/component-anatomy.md` |
| `ReactiveQuery<T>`, SSE subscriptions, loading states, optimistic updates | `references/reactive-query.md` |
| MudForm, MudTable (server-side), dialogs, ETags, search debounce | `references/forms-dialogs.md` |
| AuthorizeView, [Authorize], TenantService, tenant-aware components | `references/auth-tenant.md` |
| Common mistakes and anti-patterns | `references/pitfalls.md` |

Related skills: `../bunit/SKILL.md` (testing Blazor components), `../aspnet-sse/SKILL.md` (SSE backend implementation), `../aspnet-hybrid-cache/SKILL.md` (cache invalidation wiring).

## Canonical Page Skeleton

This is the shape every *stateful, data-loading* page follows. Read `references/component-anatomy.md` for variants and explanation.

```razor
@page "/admin/widgets"
@rendermode InteractiveServer
@implements IDisposable

@inject IWidgetsClient WidgetsClient
@inject BookStoreEventsService EventsService
@inject QueryInvalidationService InvalidationService
@inject ISnackbar Snackbar

<PageTitle>Widgets</PageTitle>

@if (_query?.IsLoading == true && _query.Data == null)
{
    <MudSkeleton />
}
else if (_query?.IsError == true)
{
    <MudAlert Severity="Severity.Error">@_query.Error</MudAlert>
}
else
{
    @* render _query.Data *@
}

@code {
    [Inject] private ILogger<Widgets> Logger { get; set; } = default!;

    private ReactiveQuery<IReadOnlyList<WidgetDto>>? _query;
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;

    protected override async Task OnInitializedAsync()
    {
        EventsService.StartListening();
        EventsService.OnNotificationReceived += HandleNotification;

        _query = new ReactiveQuery<IReadOnlyList<WidgetDto>>(
            queryFn:             ct => WidgetsClient.GetWidgetsAsync(ct),
            eventsService:       EventsService,
            invalidationService: InvalidationService,
            queryKeys:           ["Widgets"],
            onStateChanged:      () => InvokeAsync(StateHasChanged),
            logger:              Logger);

        await _query.LoadAsync(cancellationToken: _cts.Token);
    }

    private async void HandleNotification(IDomainEventNotification notification)
    {
        if (notification is PingNotification) return;
        if (InvalidationService.ShouldInvalidate(notification, ["Widgets"]))
            await InvokeAsync(async () => { await _query!.LoadAsync(silent: true, _cts.Token); });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts.Cancel();
        _cts.Dispose();
        _query?.Dispose();
        EventsService.OnNotificationReceived -= HandleNotification;
    }
}
```

## Rules at a Glance

- All stateful pages declare `@rendermode InteractiveServer`; dialogs/shared components inherit it
- Every SSE subscriber must `@implements IDisposable` and unsubscribe in `Dispose()`
- Data loading uses `ReactiveQuery<T>` — never raw `await Client.GetAsync()` in `OnInitializedAsync` without reactive wrapping
- Always use injected Refit clients (`IBookStoreClient` interfaces) — never raw `HttpClient`
- New query keys (e.g., `"Widgets"`) must be registered in `QueryInvalidationService` to receive SSE-driven invalidation
- UI mutations go through `CatalogService`/`AdminService` for optimistic update orchestration; write results directly in the page only for simple admin flows
- Forms use MudBlazor's `MudForm`/`MudTextField` — not `EditContext`/`DataAnnotations`

## Common Mistakes

See `references/pitfalls.md` for detailed before/after code. Quick list:

- **Missing IDisposable** → SSE events still fire after navigation, causing exceptions on disposed components
- **Missing `QueryInvalidationService` mapping** → SSE arrives but UI never refreshes
- **Calling `StateHasChanged()` from a non-Blazor thread** → use `InvokeAsync(StateHasChanged)` inside `HandleNotification`
- **Calling HttpClient directly** → bypasses TenantHeaderHandler and auth chain; always use Refit interfaces
- **Business logic in .razor** → move to `Services/` or backing classes
- **`async void` event handler without `try/catch`** → unhandled exceptions crash the circuit; add error handling or use `InvokeAsync<Task>`
