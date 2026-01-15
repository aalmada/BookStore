# Web Instructions (Blazor Frontend)

## 1. State Management (The "Reactive" Pattern)
- **ReactiveQuery<T>**: The primary primitive for data fetching.
  - Automatically handles SSE invalidation via `BookStoreEventsService`.
  - Usage: `new ReactiveQuery<T>(..., eventsService, invalidationService)`.

## 2. Optimistic Updates
- **Property Changes** (e.g., Toggle Favorite):
  - **Pattern**: `Mutate -> Call -> Rollback`.
  - **Code**: `query.MutateData(s => s with { Prop = newValue })`.
- **List Additions** (e.g., Create Book):
  - **Pattern**: Use `OptimisticUpdateService` singleton.
  - **Flow**: `OptimisticService.Add(...)` -> Data merges in UI -> SSE confirms -> Optimistic entry removed.

## 3. Invalidation
- **Service**: `QueryInvalidationService`.
- **Rule**: Map incoming `DomainEvent` (e.g., `BookCreated`) to `CacheKeys` (e.g., `"Books"`).

## 4. UI & Styling
- **Tailwind**: Use utility classes (via `app.css`).
- **Localization**: Display localized strings from DTOs.
- **Components**: Keep logic in Services/Backing classes; keep `.razor` clean.
