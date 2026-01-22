# Web (Blazor) Instructions

**Scope**: `src/BookStore.Web/**`

## Guides
- `docs/guides/real-time-notifications.md` - SSE & cache invalidation
- `docs/guides/caching-guide.md` - HybridCache patterns

## Skills
- `/scaffold-frontend-feature` - Add Blazor feature with reactive state
- `/debug-sse` - Debug real-time updates

## Rules
- Use `ReactiveQuery<T>` for data fetching with auto-invalidation
- Use `QueryInvalidationService` + `BookStoreEventsService` for SSE
- Use `OptimisticUpdateService` for immediate UI feedback
- MudBlazor for components; ensure accessibility
