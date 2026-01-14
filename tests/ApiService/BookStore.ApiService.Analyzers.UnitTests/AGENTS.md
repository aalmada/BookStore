# Analyzer Unit Tests Instructions

**Scope**: `tests/ApiService/BookStore.ApiService.Analyzers.UnitTests/**`

## Purpose
Unit tests for custom Roslyn analyzers that enforce architectural patterns (Event Sourcing, CQRS, Marten conventions, Wolverine patterns).

## Core Patterns
- **Test Framework**: **MUST use TUnit** for all tests (not xUnit or NUnit).
- **Assertions**: Use TUnit assertions: `await Assert.That(...).IsTrue()`, `.IsEqualTo()`, `.Contains()`, etc.
- **Analyzer Testing**: Use Roslyn analyzer test framework
- **Test Data**: Actual C# code files in `TestData` folders (not strings)
- **Diagnostics**: Verify that analyzers produce expected warnings/errors for code violations
- **Organization**: Tests grouped by diagnostic ID (BS1xxx, BS2xxx, BS3xxx, BS4xxx)

## Test Structure
- **Test Data Location**: `TestData/{DiagnosticId}/` contains actual `.cs` files
- **Expected Diagnostics**: Tests verify specific diagnostic IDs are reported at correct locations
- **Code Fixes**: Some analyzers may also test code fix providers

## Writing Analyzer Tests
1. **Create Test Data**: Add actual C# code file in appropriate `TestData` folder
2. **Good/Bad Examples**: Create both valid and invalid code samples
3. **Test Class**: Follow pattern `{DiagnosticId}AnalyzerTests.cs`
4. **Assertions**: Verify diagnostic ID, severity, and location

## Running Tests
- **All analyzer tests**: `dotnet test tests/ApiService/BookStore.ApiService.Analyzers.UnitTests/BookStore.ApiService.Analyzers.UnitTests.csproj`

## Reference
See [analyzer-rules.md](file:///Users/antaoalmada/Projects/BookStore/docs/analyzer-rules.md) for complete list of rules and their rationale.
