# AppHost Integration Tests Instructions

**Scope**: `tests/BookStore.AppHost.Tests/**`

## Guides
- `docs/guides/integration-testing-guide.md` - Integration testing patterns
- `docs/guides/testing-guide.md` - General testing patterns

## Skills
- `/scaffold-test` - Create integration test with SSE verification
- `/run-integration-tests` - Run integration tests

## Rules
- **TUnit only** (not xUnit/NUnit) - Use `[Test]` and `await Assert.That(...)`
- Use `DistributedApplicationTestingBuilder` from Aspire
- **Avoid `Task.Delay`** - Never use hardcoded delays to wait for eventual consistency
- **SSE Verification** - Use `TestHelpers.ExecuteAndWaitForEventAsync()`
- **Polling Utility** - Use `TestHelpers.WaitForConditionAsync()` for read-side checks
- **Bogus** - Use `TestHelpers.GenerateFake*Request()` for test data
- Naming: `{Feature}Tests.cs` (e.g., `BookCrudTests.cs`)
