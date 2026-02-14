# Shared Library Unit Tests Instructions

**Scope**: `tests/BookStore.Shared.UnitTests/**`

## Guides
- `docs/guides/testing-guide.md` - Testing patterns

## Skills
- `/test__unit_suite` - Run unit tests

## Rules
- **TUnit only** (not xUnit/NUnit) - Use `[Test]` and `await Assert.That(...)`
- Test DTO serialization (camelCase, ISO 8601)
- Test record immutability and equality
- Test notification serialization for SSE
- Naming: `{ModelName}Tests.cs`

## Running Tests
- **All shared tests**: `dotnet test tests/BookStore.Shared.UnitTests/`
- **Specific test**: `dotnet test --filter "FullyQualifiedName~PartialDateTests"`

## Key Test Files
- [PartialDateTests.cs](file:///Users/antaoalmada/Projects/BookStore/tests/BookStore.Shared.UnitTests/PartialDateTests.cs) - Example of comprehensive model testing
