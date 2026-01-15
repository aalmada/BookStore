# Web (Blazor) Instructions

**Scope**: `src/BookStore.Web/**`

## Core Rules
- **Components**: Use `razor` files; keep logic in code-behind or separate classes if complex.
- **State Management**: Use `ReactiveQuery`, `OptimisticUpdateService`, and `QueryInvalidationService`.

## State Management & Data Fetching
Use `ReactiveQuery<T>` for data fetching, caching, and automatic invalidation.

### ReactiveQuery Pattern
1.  **Inject Services**:
    - `BookStoreEventsService` (for SSE)
    - `QueryInvalidationService` (for determining invalidation)
    - `OptimisticUpdateService` (if creating entities optimistically)
2.  **Initialize**:
    - Create `ReactiveQuery<T>` in `OnInitializedAsync`.
    - Define `queryKeys` (e.g., `["Books", "Authors"]`) to control invalidation.
    - Subscribe to `EventsService` and `OptimisticService` (if needed).
3.  **Load Data**: Call `await query.LoadAsync()` (initial) or `await query.LoadAsync(silent: true)` (background).
4.  **Optimistic Updates**:
    - Use `query.MutateData(data => ...)` to immediately update UI.
    - Perform API call.
    - On failure, rollback using `query.MutateData` again.
5.  **Disposal**: Implement `IAsyncDisposable` and dispose the query and event subscriptions.

### Example
```csharp
protected override async Task OnInitializedAsync()
{
    EventsService.StartListening();
    
    bookQuery = new ReactiveQuery<PagedListDtoOfBookDto>(
        queryFn: FetchBooksAsync,
        eventsService: EventsService,
        invalidationService: InvalidationService,
        queryKeys: new[] { "Books" }, 
        onStateChanged: StateHasChanged,
        logger: Logger
    );
    
    await bookQuery.LoadAsync();
}
```
- **Styling**: Use MudBlazor components and utilities.
- **Localization**: Ensure all UI text is localizable.

## Considerations
- **Accessibility**: Ensure high accessibility standards.
- **Responsiveness**: Design for mobile and desktop.
