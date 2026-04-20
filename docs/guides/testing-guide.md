# Testing Guide

This guide covers unit and project-level testing conventions in BookStore.

For end-to-end Aspire integration tests, see `docs/guides/integration-testing-guide.md`.

## Framework and Rules

BookStore tests use **TUnit**.

Required patterns:

- Use `[Test]` methods with async `Task`
- Use `await Assert.That(...)` assertions
- Use **NSubstitute** for mocking
- Use **Bogus**-based helpers for generated test data
- Avoid `Task.Delay`/`Thread.Sleep` in async consistency checks

## Test Projects

Current test projects in repository:

- `tests/BookStore.ApiService.UnitTests/`
- `tests/BookStore.ApiService.Analyzers.UnitTests/`
- `tests/BookStore.Shared.UnitTests/`
- `tests/BookStore.Web.Tests/`
- `tests/BookStore.AppHost.Tests/` (integration/system tests)

## Running Tests

### Run all tests

```bash
dotnet test
```

### Run a single test project

```bash
dotnet test tests/BookStore.ApiService.UnitTests/BookStore.ApiService.UnitTests.csproj
```

### Limit TUnit parallelism

Pass TUnit arguments after `--`:

```bash
dotnet test -- --maximum-parallel-tests 4
```

### Filter by category (TUnit tree filter)

```bash
dotnet test -- --treenode-filter "/*/*/*/*[Category=Integration]"
```

## Unit Test Patterns

### Handler tests

- Arrange command + substituted dependencies
- Execute handler directly
- Assert result and interaction calls

### Serialization/validation tests

- Verify JSON shape, date/time formats, and contract behavior
- Validate edge cases around options, parsing, and model constraints

### Analyzer tests

- Keep sample source minimal and focused
- Assert expected diagnostics and locations

## Integration Test Boundary

`tests/BookStore.AppHost.Tests/` runs against real infrastructure started by Aspire (PostgreSQL, Redis, Azurite, API, Web). These are not unit tests.

Key helpers live under:

- `tests/BookStore.AppHost.Tests/Helpers/`
- `tests/BookStore.AppHost.Tests/TestConstants.cs`
- `tests/BookStore.AppHost.Tests/GlobalSetup.cs`

## Coverage

You can collect coverage with standard `dotnet test` collectors, for example:

```bash
dotnet test --collect:"XPlat Code Coverage"
```

Coverage artifacts are written under each project's `TestResults/` directory.

## Common Mistakes

- Using xUnit/NUnit attributes (`[Fact]`, `[TestCase]`) in TUnit projects
- Forgetting `await` on `Assert.That(...)`
- Hardcoding shared IDs/data across tests
- Bypassing helper abstractions and duplicating setup logic
