# Analyzer Unit Tests Instructions

**Scope**: `tests/BookStore.ApiService.Analyzers.UnitTests/**`

## Guides
- `docs/guides/analyzer-rules.md` - BS1xxx-BS4xxx rules
- `docs/guides/testing-guide.md` - Testing patterns

## Skills
- `/test__unit_suite` - Run unit tests

## Rules
- **TUnit only** (not xUnit/NUnit) - Use `[Test]` and `await Assert.That(...)`
- Test data in `TestData/{DiagnosticId}/` as actual `.cs` files
- Verify diagnostic IDs at correct locations
- Naming: `{DiagnosticId}AnalyzerTests.cs`
