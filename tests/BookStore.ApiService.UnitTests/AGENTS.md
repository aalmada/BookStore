# ApiService Unit Tests Instructions

**Scope**: `tests/ApiService/BookStore.ApiService.UnitTests/**`

## Purpose
Unit tests for business logic, handlers, validation, and domain logic in the ApiService. Tests individual components in isolation without spinning up the full application stack.

## Core Patterns
- **Test Framework**: **MUST use TUnit** for all tests (not xUnit or NUnit).
- **Assertions**: Use TUnit assertions: `await Assert.That(...).IsTrue()`, `.IsEqualTo()`, `.IsNotNull()`, etc.
- **Handler Testing**: Test Wolverine handlers with mocked dependencies
- **Aggregate Testing**: Verify aggregate behavior methods return correct events
- **Validation Testing**: Ensure business rules are enforced
- **Fast Execution**: No database, no HTTP - pure unit tests

## Writing Unit Tests
1. **Test Naming**: `{Component}Tests.cs` or `{Feature}HandlerTests.cs`
2. **Arrange-Act-Assert**: Follow AAA pattern consistently
3. **Mocking**: Use mocks for external dependencies (IDocumentSession, etc.)
4. **Event Verification**: Assert that correct events are returned from aggregates

## Running Tests
- **All unit tests**: `dotnet test tests/ApiService/BookStore.ApiService.UnitTests/BookStore.ApiService.UnitTests.csproj`
