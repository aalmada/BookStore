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
✅ OptimisticUpdateService for writes  ❌ UI waits for server roundtrip
✅ TenantService for tenant context    ❌ Hardcoded tenant or missing headers
```

## Common Mistakes
- ❌ Calling HttpClient directly → Use injected BookStore.Client interfaces
- ❌ Missing SSE invalidation mapping → Update QueryInvalidationService rules
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
