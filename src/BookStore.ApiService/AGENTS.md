# ApiService — Agent Instructions

## Quick Reference
- **Scope**: `src/BookStore.ApiService/**`
- **Stack**: .NET 10, C# 14, Marten, Wolverine, HybridCache
- **Docs**: `docs/guides/`
- **Run**: `aspire run` | **Test**: `dotnet test` | **Format**: `dotnet format`

## Key Rules (MUST follow)
```
✅ record BookAdded(...)          ❌ record AddBook(...)
✅ Guid.CreateVersion7()          ❌ Guid.NewGuid()
✅ DateTimeOffset.UtcNow          ❌ DateTime.Now
✅ [LoggerMessage(...)]           ❌ _logger.LogInformation(...) / LogWarning / LogError
✅ Result.Failure(Error.X(...))   ❌ throw new Exception() / return BadRequest()
✅ Wolverine-managed projections  ❌ Marten async daemon
✅ Projections stay async by default ❌ Switching to inline without explicit need
✅ Tenant-scoped sessions          ❌ Cross-tenant queries
✅ ETags for concurrency           ❌ Blind writes
✅ Cache tags for invalidation     ❌ Stale cache after mutations
```

**Logging**: Use `/lang__logger_message` skill for all logging. Logs organized by domain in `Infrastructure/Logging/Log.<Domain>.cs`.

**Error Handling**: Use `/lang__problem_details` skill for all errors. Return `Result.Failure(Error.<Type>(code, message)).ToProblemDetails()`.

**CRITICAL**: ALL failures MUST return RFC 7807 ProblemDetails with a machine-readable error code. This includes endpoints, handlers, AND middleware. Never return plain JSON errors.

## Common Mistakes
- ❌ Business logic in endpoints → Put logic in aggregates/handlers
- ❌ Missing SSE notification → Add to `MartenCommitListener`
- ❌ Missing cache invalidation → Call `RemoveByTagAsync` after mutations
- ❌ Manually running Marten async daemon → Async projections are updated by Wolverine
- ❌ Skipping tenant context → Use tenant-scoped sessions and cache keys
- ❌ Ignoring ETag checks → Use `IHaveETag` and `ETagHelper`
- ❌ Returning plain JSON errors → ALL failures must return ProblemDetails with error codes (endpoints, handlers, middleware)

## Project Layout
| Path | Purpose |
|------|---------|
| `src/BookStore.ApiService/Aggregates/` | Event-sourced aggregates
| `src/BookStore.ApiService/Commands/` | Command records
| `src/BookStore.ApiService/Events/` | Event records
| `src/BookStore.ApiService/Handlers/` | Wolverine handlers (write model)
| `src/BookStore.ApiService/Endpoints/` | Minimal API endpoints
| `src/BookStore.ApiService/Projections/` | Marten projections (async)
| `src/BookStore.ApiService/Infrastructure/` | Marten/Wolverine configuration, middleware, caching, SSE

## Major Patterns
- Conjoined multi-tenancy; sessions use `ITenantContext` or message tenant IDs
- Commands and events are `record` types; handlers coordinate, aggregates enforce invariants
- HybridCache with tag-based invalidation after mutations
- ETags for optimistic concurrency (`IHaveETag`, `ETagHelper`)
- SSE notifications emitted from `ProjectionCommitListener` on projection commits
- Correlation/causation IDs propagated via Marten metadata and Wolverine middleware

## Quick Troubleshooting
- **SSE not firing**: Ensure `ProjectionCommitListener` handles the projection type
- **Cache stale**: Confirm `RemoveByTagAsync` after mutations
- **Projection lag**: Async projections are Wolverine-managed, not Marten daemon
- **ETag mismatch**: Ensure `IHaveETag` is set and `If-Match` is supplied

## Documentation Index
| Topic | Guide |
|-------|-------|
| Event Sourcing | `docs/guides/event-sourcing-guide.md` |
| Marten | `docs/guides/marten-guide.md` |
| Wolverine | `docs/guides/wolverine-guide.md` |
| Caching | `docs/guides/caching-guide.md` |
| Real-time | `docs/guides/real-time-notifications.md` |
| Analyzer Rules | `docs/guides/analyzer-rules.md` |
