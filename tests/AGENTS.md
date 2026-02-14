# Tests — Agent Instructions

## Quick Reference
- **Stack**: .NET 10, TUnit, Bogus, NSubstitute
- **Docs**: `docs/guides/testing-guide.md`, `docs/guides/integration-testing-guide.md`
- **Test**: `dotnet test` | **Project**: `dotnet test --project tests/<Project>/<Project>.csproj`
- **Helpers**: `tests/BookStore.AppHost.Tests/TestHelpers.cs`

## Key Rules (MUST follow)
```
✅ [Test] async Task (TUnit)      ❌ [Fact] / NUnit attributes
✅ await Assert.That(...)         ❌ FluentAssertions or other assert libs
✅ Bogus for test data            ❌ Hand-rolled random data
✅ NSubstitute for mocking        ❌ Moq / other mocking frameworks
✅ Per-test setup                 ❌ Rely on seeded/global init data
✅ WaitForConditionAsync / SSE    ❌ Task.Delay / Thread.Sleep
```

## Common Mistakes
- ❌ Mixing xUnit/NUnit with TUnit → Use TUnit attributes and assertions only
- ❌ Sharing mutable state between tests → Create all data inside each test
- ❌ Hardcoded delays for eventual consistency → Use `TestHelpers.WaitForConditionAsync`
- ❌ Skipping SSE verification on writes → Use `TestHelpers.ExecuteAndWaitForEventAsync`
- ❌ Single-case tests for variable inputs → Prefer data-driven tests to cover more cases

## Project Layout
| Path | Purpose |
|------|---------|
| `tests/BookStore.AppHost.Tests/` | Aspire-hosted integration tests
| `tests/BookStore.ApiService.UnitTests/` | API service unit tests
| `tests/BookStore.Web.Tests/` | Blazor/web-focused tests
| `tests/BookStore.Shared.UnitTests/` | Shared models and utilities tests
| `tests/BookStore.ApiService.Analyzers.UnitTests/` | Roslyn analyzer tests

## Skills
| Category | Skills |
|----------|--------|
| **Scaffold** | `/test__integration_scaffold` |
| **Verify** | `/test__unit_suite`, `/test__integration_suite`, `/test__verify_feature` |

## Quick Troubleshooting
- **Flaky tests**: Ensure per-test data creation and unique IDs
- **Write-side timing**: Prefer SSE helpers over polling or delays
- **Analyzer tests failing**: Keep `TestData` inputs out of compilation

## Documentation Index
| Topic | Guide |
|-------|-------|
| Testing | `docs/guides/testing-guide.md` |
| Integration Testing | `docs/guides/integration-testing-guide.md` |
