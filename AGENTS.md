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
| **Run** | `/aspire__start_solution`, `/aspire__setup_mcp` |
| **Scaffold** | `/wolverine__create_operation`, `/wolverine__update_operation`, `/wolverine__delete_operation`, `/marten__get_by_id`, `/marten__list_query`, `/marten__aggregate_scaffold`, `/marten__single_stream_projection`, `/marten__multi_stream_projection`, `/marten__composite_projection`, `/marten__event_projection`, `/frontend__feature_scaffold`, `/test__integration_scaffold` |
| **Verify** | `/test__verify_feature`, `/test__unit_suite`, `/test__integration_suite` |
| **Debug** | `/frontend__debug_sse`, `/cache__debug_cache` |
| **Deploy** | `/deploy__azure_container_apps`, `/deploy__kubernetes_cluster`, `/deploy__rollback` |
| **Utility** | `/ops__doctor_check`, `/ops__rebuild_clean`, `/meta__create_skill`, `/meta__cheat_sheet` |
| **Documentation** | `/lang__docfx_guide`, `/meta__write_agents_md` |

**Aliases**: `/sco`→wolverine__create_operation, `/suo`→wolverine__update_operation, `/sdo`→wolverine__delete_operation, `/sgbi`→marten__get_by_id, `/slq`→marten__list_query, `/sa`→marten__aggregate_scaffold, `/ssp`→marten__single_stream_projection, `/msp`→marten__multi_stream_projection, `/scp`→marten__composite_projection, `/sep`→marten__event_projection, `/st`→test__integration_scaffold, `/vf`→test__verify_feature

## Quick Troubleshooting
- **Build fails**: Check BS1xxx-BS4xxx analyzer errors first
- **SSE not working**: Run `/frontend__debug_sse`
- **Cache stale**: Run `/cache__debug_cache`
- **Environment issues**: Run `/ops__doctor_check`

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
