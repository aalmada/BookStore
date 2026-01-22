# ApiService Unit Tests Instructions

**Scope**: `tests/BookStore.ApiService.UnitTests/**`

## Guides
- `docs/guides/testing-guide.md` - Testing patterns

## Skills
- `/run-unit-tests` - Run unit tests

## Rules
- **TUnit only** (not xUnit/NUnit) - Use `[Test]` and `await Assert.That(...)`
- Test handlers with mocked dependencies (IDocumentSession, etc.)
- Verify aggregates return correct events from behavior methods
- AAA pattern: Arrange-Act-Assert
