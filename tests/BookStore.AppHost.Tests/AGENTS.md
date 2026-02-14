# AppHost Integration Tests Instructions

**Scope**: `tests/BookStore.AppHost.Tests/**`

## Guides
- `docs/guides/integration-testing-guide.md` - Integration testing patterns
- `docs/guides/testing-guide.md` - General testing patterns

## Skills
- `/test__integration_scaffold` - Create integration test with SSE verification
- `/test__integration_suite` - Run integration tests

## Rules
- **TUnit only** (not xUnit/NUnit) - Use `[Test]` and `await Assert.That(...)`
- Use `DistributedApplicationTestingBuilder` from Aspire
- **Avoid `Task.Delay`** - Never use hardcoded delays to wait for eventual consistency
- **SSE Verification** - Use `TestHelpers.ExecuteAndWaitForEventAsync()`.
- **Avoid Redundant Polling** - Do not use `WaitForConditionAsync()` after an event-driven helper (e.g., `CreateBookAsync`, `AddToFavoritesAsync`) as these already guarantee read-side consistency.
- **Polling Utility** - Use `TestHelpers.WaitForConditionAsync()` for read-side checks only when an event-driven helper is not available.
- **Bogus** - Use `TestHelpers.GenerateFake*Request()` for test data
- Naming: `{Feature}Tests.cs` (e.g., `BookCrudTests.cs`)
