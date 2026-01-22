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
- Use `TestHelpers.ExecuteAndWaitForEventAsync()` for SSE verification
- Use `Bogus` via `TestHelpers.GenerateFake*Request()` for test data
- Naming: `{Feature}Tests.cs` (e.g., `BookCrudTests.cs`)
