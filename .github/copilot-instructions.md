# BookStore Project Instructions

## 1. Core Principles
- **Clean Architecture**: Strictly separate concerns.
- **English Language**: All code, comments, and documentation must be in English.
- **Conciseness**: Avoid verbose boilerplate. Use modern C# features (records, pattern matching).
- **No "I"**: Do not refer to yourself. Output code directly.

## 2. Formatting & Style
- **File Scoped Namespaces**: Always use `namespace BookStore.Namespace;` (no braces).
- **Formatting**: Follow standard .NET coding conventions (`dotnet format`).
- **Ordering**:
  1. Fields
  2. Constructors
  3. Properties (public then private)
  4. Methods (public then private)

## 3. Testing
- **Integration Tests**: Prefer integration tests (`BookStore.AppHost.Tests`) over mocking.
- **Test Names**: Suggest descriptive names, e.g., `Should_ReturnExpectedResult_When_ConditionMet`.
- **Assertions**: Use strict assertions (check specific properties, not just `NotNull`).

## 4. Documentation
- **XML Comments**: Add `///` comments to all public APIs, Commands, and Events.
- **Diagrams**: Use Mermaid for complex logic flows.
