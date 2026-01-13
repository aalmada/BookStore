# Web Instructions (Blazor Frontend)

## 1. UI Components (Blazor)
- **Logic**: Avoid complex logic in `.razor` files. Move to services or backing classes.
- **Client**: Inject specific Refit clients (e.g., `IBooksClient`), NOT `HttpClient` or `ISystemClient` directly.
- **Aesthetics**: Use Tailwind CSS (via `app.css`). Avoid inline styles.

## 2. Localization
- **Strings**: Use resolved string properties from DTOs (e.g., `Description`) for display.
- **Formatting**: Use `CultureInfo.CurrentCulture` for dates/numbers.
- **Switching**: Use `LocalizationService` if available, or force reload/cookie for culture switch.

## 3. State Management
- **SSE**: Use `BookStoreEventsService` (in Client lib) for listening to streams.
- **Cache**: Rely on Server-Side `HybridCache` (via API responses). Front-end caching should be minimal.
- **Invalidation**: `QueryInvalidationService` maps Events (e.g., `BookCreated`) to Cache Keys (e.g., `"Books"`).

## 4. Data & State Patterns
- **Fetching**: Use `ReactiveQuery<T>` (automatic SSE invalidation).
- **Optimistic Property Updates** (e.g., Favorites):
  - **Pattern**: Mutate -> Call -> Rollback using `query.MutateData()`.
- **Optimistic List Additions** (e.g., New Book):
  - **Service**: Inject `OptimisticUpdateService`.
  - **Pattern**: `AddOptimisticBook(...)` -> API Call. The service manages the temporary state until the backend event confirms it.
