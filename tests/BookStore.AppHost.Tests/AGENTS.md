# AppHost Integration Tests — Agent Instructions

## Quick Reference
- **Scope**: `tests/BookStore.AppHost.Tests/**`
- **Docs**: `docs/guides/integration-testing-guide.md`, `docs/guides/testing-guide.md`
- **Test**: `dotnet test tests/BookStore.AppHost.Tests/`
- **Filter**: `dotnet test --filter "FullyQualifiedName~BookCrudTests"`
- **Helpers**: `tests/BookStore.AppHost.Tests/TestHelpers.cs`
- **Playwright browsers**: Must be installed before first run — see setup note below

## Key Rules (MUST follow)
```
✅ [Test] async Task (TUnit)      ❌ xUnit / NUnit attributes
✅ await Assert.That(...)         ❌ FluentAssertions or other assert libs
✅ Use Aspire infra               ❌ Ad-hoc external setup
✅ Await SSE event per command    ❌ Polling or any delays
✅ Per-test setup                 ❌ Rely on seeded/global init data
✅ Data-driven tenant coverage    ❌ Single-tenant-only tests
✅ Verify tenant isolation        ❌ Shared data across tenants
```

## Playwright Setup

These tests use **Microsoft.Playwright** for browser-based authentication flows. Playwright browsers must be installed separately after building the project:

```bash
dotnet build tests/BookStore.AppHost.Tests/BookStore.AppHost.Tests.csproj
node tests/BookStore.AppHost.Tests/bin/Debug/net10.0/.playwright/package/index.js install chromium
```

> [!IMPORTANT]
> The `node` command path is relative to the repo root. The `.playwright` directory is created by the build; run the build step first. Re-run after a `dotnet clean` or switching build configurations (`Debug`/`Release`).

## Common Mistakes
- ❌ Ignoring tests/AGENTS.md rules → This file extends `tests/AGENTS.md`
- ❌ Running browser tests without installing Playwright browsers → Run the install step above first
- ❌ Using polling/delays for async commands → Always await SSE events
- ❌ Redundant polling after event helpers → `ExecuteAndWaitForEventAsync` already ensures consistency
- ❌ Skipping infra startup → Use Aspire `DistributedApplicationTestingBuilder`

## Project Layout
| Path | Purpose |
|------|---------|
| `tests/BookStore.AppHost.Tests/TestHelpers.cs` | SSE and command helpers
| `tests/BookStore.AppHost.Tests/GlobalSetup.cs` | Aspire app lifecycle hooks
| `tests/BookStore.AppHost.Tests/Data/` | Test data helpers
| `tests/BookStore.AppHost.Tests/Services/` | Test service fixtures

## Skills
| Category | Skills |
|----------|--------|
| **Scaffold** | `/test__integration_scaffold` |
| **Verify** | `/test__integration_suite` |

## Local Testing Patterns
- Aspire-hosted tests use `DistributedApplicationTestingBuilder` and `GlobalHooks`
- Refit clients are created with `RestService.For<T>` over test HTTP clients
- Async commands emit SSE events; always use `TestHelpers.ExecuteAndWaitForEventAsync`
- Data generation comes from `TestHelpers.GenerateFake*Request()` (Bogus)
- All tests must be data-driven to run against the default tenant and one non-default tenant
- Each test must assert tenant isolation (data created in one tenant is not visible in the other)

## Documentation Index
| Topic | Guide |
|-------|-------|
| Integration Testing | `docs/guides/integration-testing-guide.md` |
| Testing | `docs/guides/testing-guide.md` |
