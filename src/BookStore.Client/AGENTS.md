# Client SDK — Agent Instructions

## Quick Reference
- **Scope**: `src/BookStore.Client/**`
- **Stack**: Refit, .NET 10, C# 14
- **Docs**: `docs/guides/api-client-generation.md`, `docs/guides/api-conventions-guide.md`
- **Register**: `AddBookStoreClient`, `AddBookStoreEvents`

## Key Rules (MUST follow)
```
✅ Refit interfaces per endpoint  ❌ Hand-written HttpClient wrappers
✅ Keep clients in sync with API  ❌ Stale client after API changes
✅ ETag headers for writes        ❌ Blind writes without If-Match
✅ Accept-Language on localized   ❌ Missing culture headers
✅ Correlation/Causation headers  ❌ Missing tracing context
✅ Tenant header on requests       ❌ Cross-tenant calls
```

## Common Mistakes
- ❌ Skipping updates after API changes → Update Refit contracts immediately
- ❌ Missing ETag handling → Use `ETagHelper` and `If-Match` headers
- ❌ Bypassing DI registration → Use `AddBookStoreClient` / `AddBookStoreEvents`
- ❌ Missing locale headers → Use `Accept-Language` when endpoints localize
- ❌ Missing tenant header → Provide tenant context per request

## Project Layout
| Path | Purpose |
|------|---------|
| `src/BookStore.Client/I*Client.cs` | Refit interfaces per endpoint
| `src/BookStore.Client/Contracts.cs` | DTO contracts (records)
| `src/BookStore.Client/ETagHelper.cs` | ETag parsing and formatting
| `src/BookStore.Client/Infrastructure/BookStoreHeaderHandler.cs` | Accept-Language, correlation/causation headers
| `src/BookStore.Client/BookStoreClientExtensions.cs` | DI registration and setup
| `src/BookStore.Client/BookStoreEventsService.cs` | SSE event listener
| `src/BookStore.Client/Services/ClientContextService.cs` | Correlation/causation context

## Major Patterns
- Refit interfaces expose `If-Match`/`If-None-Match` for concurrency and caching
- Accept-Language is optional but supported for localized endpoints
- Correlation and causation IDs are injected via `BookStoreHeaderHandler`
- Tenant context is carried via headers on multi-tenant endpoints
- SSE events update causation IDs through `BookStoreEventsService`

## Quick Troubleshooting
- **412 Precondition Failed**: Ensure `If-Match` uses the latest ETag
- **Missing localization**: Provide `Accept-Language` header
- **Tracing gaps**: Ensure `ClientContextService` and header handler are registered
- **Unexpected tenant data**: Ensure tenant header is set for the request

## Documentation Index
| Topic | Guide |
|-------|-------|
| API Client Generation | `docs/guides/api-client-generation.md` |
| API Conventions | `docs/guides/api-conventions-guide.md` |
