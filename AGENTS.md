# BookStore — Agent Instructions

## Quick Reference
- **Stack**: .NET 10, C# 14, Marten, Wolverine, HybridCache, Aspire
- **Docs**: `docs/getting-started.md`, `docs/guides/`
- **Run**: `aspire run` | **Test**: `dotnet test` | **Format**: `dotnet format`

## Key Rules (MUST follow)
```
✅ Guid.CreateVersion7()          ❌ Guid.NewGuid()
✅ DateTimeOffset.UtcNow          ❌ DateTime.Now
✅ record BookAdded(...)          ❌ record AddBook(...)
✅ namespace BookStore.X;         ❌ namespace BookStore.X { }
✅ [Test] async Task (TUnit)      ❌ [Fact] (xUnit)
✅ WaitForConditionAsync          ❌ Task.Delay / Thread.Sleep
```

## Common Mistakes
- ❌ Business logic in endpoints → Put in aggregates/handlers
- ❌ Forgetting SSE notification → Add to `MartenCommitListener`
- ❌ Missing cache invalidation → Call `RemoveByTagAsync` after mutations
- ❌ Using xUnit/NUnit → Use TUnit with `await Assert.That(...)`
- ❌ Hardcoded `Task.Delay` in tests → Use `TestHelpers.WaitForConditionAsync` or SSE listeners

## Project Layout
| Path | Purpose |
|------|---------|
| `src/BookStore.ApiService/` | Backend API |
| `src/BookStore.Web/` | Blazor Frontend |
| `src/BookStore.Client/` | API Client SDK |
| `src/BookStore.Shared/` | Shared DTOs & Notifications |
| `src/BookStore.AppHost/` | Aspire orchestration |
| `tests/` | Unit & integration tests |

## Skills

| Category | Skills |
|----------|--------|
| **Run** | `/start-solution`, `/setup-aspire-mcp` |
| **Scaffold** | `/scaffold-create-operation`, `/scaffold-update-operation`, `/scaffold-delete-operation`, `/scaffold-get-by-id`, `/scaffold-list-query`, `/scaffold-aggregate`, `/scaffold-single-stream-projection`, `/scaffold-multi-stream-projection`, `/scaffold-composite-projection`, `/scaffold-event-projection`, `/scaffold-frontend-feature`, `/scaffold-test` |
| **Verify** | `/verify-feature`, `/run-unit-tests`, `/run-integration-tests` |
| **Debug** | `/debug-sse`, `/debug-cache` |
| **Deploy** | `/deploy-to-azure`, `/deploy-kubernetes`, `/rollback-deployment` |
| **Utility** | `/doctor`, `/rebuild-clean`, `/scaffold-skill`, `/cheat-sheet` |
| **Documentation** | `/write-documentation-guide`, `/write-agents-md` |

**Aliases**: `/sco`→scaffold-create-operation, `/suo`→scaffold-update-operation, `/sdo`→scaffold-delete-operation, `/sgbi`→scaffold-get-by-id, `/slq`→scaffold-list-query, `/sa`→scaffold-aggregate, `/ssp`→scaffold-single-stream-projection, `/msp`→scaffold-multi-stream-projection, `/scp`→scaffold-composite-projection, `/sep`→scaffold-event-projection, `/st`→scaffold-test, `/vf`→verify-feature

## Quick Troubleshooting
- **Build fails**: Check BS1xxx-BS4xxx analyzer errors first
- **SSE not working**: Run `/debug-sse`
- **Cache stale**: Run `/debug-cache`
- **Environment issues**: Run `/doctor`

## Documentation Index
| Topic | Guide |
|-------|-------|
| Setup | `docs/getting-started.md` |
| Architecture | `docs/architecture.md` |
| Event Sourcing | `docs/guides/event-sourcing-guide.md` |
| Marten | `docs/guides/marten-guide.md` |
| Wolverine | `docs/guides/wolverine-guide.md` |
| Caching | `docs/guides/caching-guide.md` |
| SSE/Real-time | `docs/guides/real-time-notifications.md` |
| Testing | `docs/guides/testing-guide.md`, `docs/guides/integration-testing-guide.md` |
| Deployment | `docs/guides/aspire-deployment-guide.md` |

## Integration Testing Guidelines
- **Avoid `Task.Delay`**: Never use hardcoded delays to wait for eventual consistency.
- **Use SSE Listeners**: Use `TestHelpers.ExecuteAndWaitForEventAsync` to wait for specific domain events.
- **Polling Utility**: Use `TestHelpers.WaitForConditionAsync` for polling the read side or search index.
- **Shared Helpers**: Prefer `TestHelpers.CreateBookAsync` etc., which already handle SSE/polling correctly.
