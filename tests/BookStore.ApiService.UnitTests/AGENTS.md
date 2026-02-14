# ApiService Unit Tests Instructions

**Scope**: `tests/BookStore.ApiService.UnitTests/**`

## Guides
- `docs/guides/testing-guide.md` - Testing patterns

## Skills
- `/test__unit_suite` - Run unit tests

## Rules
- **TUnit only** (not xUnit/NUnit) - Use `[Test]` and `await Assert.That(...)`
- **Avoid `Task.Delay`** - Use mocks and direct execution for predictable tests
- Test handlers with mocked dependencies (IDocumentSession, etc.)
- Verify aggregates return correct events from behavior methods
- AAA pattern: Arrange-Act-Assert
