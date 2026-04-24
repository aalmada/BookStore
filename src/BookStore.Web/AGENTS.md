# BookStore Web (Blazor) — Agent Instructions

## Quick Reference
- **Stack**: .NET 10, C# 14, Blazor Server, MudBlazor, Refit, Polly
- **Docs**: `docs/guides/real-time-notifications.md`, `docs/guides/caching-guide.md`
- **Run**: `dotnet run --project src/BookStore.Web/` | **Test**: `dotnet test tests/BookStore.Web.Tests/` | **Format**: `dotnet format`

## Key Rules (MUST follow)
```
✅ Use BookStore.Client Refit clients  ❌ Call API endpoints directly
✅ ReactiveQuery<T> for reads          ❌ Manual data fetch without invalidation
✅ QueryInvalidationService + SSE      ❌ Polling for updates
✅ MudTable Items=@(_query?.Data)      ❌ MudTable ServerData= on SSE-driven pages
✅ ReactiveQuery.MutateAsync via CatalogService ❌ setOptimistic/setRollback lambdas in .razor
✅ Keep query data as source of truth   ❌ Reintroduce OptimisticUpdateService ghost-state cache
✅ _query.InvalidateAsync() after dialogs ❌ _table.ReloadServerData() after mutations
✅ TenantService for tenant context    ❌ Hardcoded tenant or missing headers
```

## Common Mistakes
- ❌ Calling HttpClient directly → Use injected BookStore.Client interfaces
- ❌ `MudTable ServerData=` on SSE-driven admin pages → SSE-triggered reloads bypass `ServerData`; use `ReactiveQuery<IReadOnlyList<T>>` + `Items=@(_query?.Data)` instead
- ❌ Missing SSE invalidation mapping → Update `QueryInvalidationService`; if the entity is stored inside a parent projection (e.g., sales inside `BookSearchProjection`), the parent notification must also yield the child query key
- ❌ Re-implementing optimistic logic in components → Use `CatalogService` methods that call `ReactiveQuery.MutateAsync` and centralize rollback
- ❌ Forcing immediate `LoadAsync()` after delete/restore → async projections can be stale; keep optimistic state and let SSE invalidation refresh
- ❌ Using `MutateData` for dialog-based mutations → for dialog workflows, keep `_query.InvalidateAsync()` so server response remains the source of truth
- ❌ Optimistic removal that leaves stale items after SSE → `MutateData` removes the row immediately; SSE then triggers a silent background `InvalidateAsync()` via `ReactiveQuery`, which replaces the local state; no extra reload needed
- ❌ Business logic in .razor files → Move to Services/ or backing classes
- ❌ Tenant mismatch in UI → Use TenantService and tenant-aware clients

## Project Layout
| Path | Purpose |
|------|---------|
| `src/BookStore.Web/Components/` | Pages and UI components |
| `src/BookStore.Web/Services/` | ReactiveQuery, SSE, tenant, and domain services |
| `src/BookStore.Web/Infrastructure/` | Middleware, headers, and client wiring |
| `src/BookStore.Web/Logging/` | Logging helpers and event messages |
| `src/BookStore.Web/wwwroot/` | Static assets and styles |

## Skills
| Category | Skills |
|----------|--------|
| **Scaffold** | `/frontend__feature_scaffold` |
| **Debug** | `/frontend__debug_sse` |

## Quick Troubleshooting
- **SSE not updating UI**: Run `/frontend__debug_sse`
- **Cache not invalidating**: Check QueryInvalidationService + BookStoreEventsService
- **Tenant-specific data incorrect**: Verify TenantService and client headers

## Documentation Index
| Topic | Guide |
|-------|-------|
| Real-time notifications | `docs/guides/real-time-notifications.md` |
| Caching patterns | `docs/guides/caching-guide.md` |
