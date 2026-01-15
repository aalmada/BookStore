# Shared Library Unit Tests Instructions

**Scope**: `tests/BookStore.Shared.UnitTests/**`

## Purpose
Unit tests for shared contracts, DTOs, and utilities used across API, Web, and Client projects. These tests ensure that shared models serialize correctly, validate properly, and maintain immutability.

## Core Patterns
- **Test Framework**: **MUST use TUnit** for all tests (not xUnit or NUnit).
- **Assertions**: Use TUnit assertions: `await Assert.That(...).IsTrue()`, `.IsEqualTo()`, `.IsNotNull()`, etc.
- **DTO Testing**: Test JSON serialization/deserialization with System.Text.Json
- **Validation Testing**: Verify data annotations and business rules
- **Immutability**: Ensure record types cannot be mutated after creation

## Test Categories

### DTO Serialization Tests
Test that DTOs serialize and deserialize correctly:
- **JSON Format**: Verify camelCase property names
- **Date Handling**: Ensure ISO 8601 format for DateTimeOffset
- **Nullability**: Test nullable reference types work correctly
- **Round-trip**: Serialize → Deserialize → Compare

### Model Tests
Test domain models and value objects:
- **PartialDate**: Test parsing, comparison, and validation (see `PartialDateTests.cs`)
- **Validation**: Ensure business rules are enforced
- **Equality**: Test record equality (`==`, `.Equals()`)
- **Immutability**: Verify `with` expressions create new instances

### Notification Tests
Test `IDomainEventNotification` implementations:
- **Serialization**: Ensure notifications can be sent over SSE
- **Type Safety**: Verify generic type parameters
- **Required Fields**: Test that all required data is present

## Writing New Tests
1. **Create Test Class**: Follow pattern `{ModelName}Tests.cs`
2. **Use TUnit**: Decorate with `[Test]` attribute
3. **Arrange-Act-Assert**: Follow AAA pattern consistently
4. **Test Data**: Use realistic examples, not just `null` or empty strings
5. **Edge Cases**: Test boundary conditions and invalid inputs

## Example Test Structure
```csharp
public class BookDtoTests
{
    [Test]
    public async Task SerializesToCamelCase()
    {
        // Arrange
        var dto = new BookDto(Id: Guid.NewGuid(), Title: "Clean Code");
        
        // Act
        var json = JsonSerializer.Serialize(dto);
        
        // Assert
        await Assert.That(json).Contains("\"id\":");
        await Assert.That(json).Contains("\"title\":");
    }
}
```

## Running Tests
- **All shared tests**: `dotnet test tests/BookStore.Shared.UnitTests/`
- **Specific test**: `dotnet test --filter "FullyQualifiedName~PartialDateTests"`

## Key Test Files
- [PartialDateTests.cs](file:///Users/antaoalmada/Projects/BookStore/tests/BookStore.Shared.UnitTests/PartialDateTests.cs) - Example of comprehensive model testing
