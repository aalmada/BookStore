# Web Frontend Unit Tests Instructions

**Scope**: `tests/BookStore.Web.Tests/**`

## Purpose
Unit tests for Blazor web application services and state management logic without spinning up the full application. These tests verify frontend logic in isolation.

## Core Patterns
- **Test Framework**: **MUST use TUnit** for all tests (not xUnit or NUnit).
- **Assertions**: Use TUnit assertions: `await Assert.That(...).IsTrue()`, `.IsEqualTo()`, `.Contains()`, etc.
- **Service Testing**: Test QueryInvalidationService, OptimisticUpdateService in isolation
- **Mocking**: Mock `IBooksClient`, `IAuthorsClient`, and other API clients
- **State Management**: Test reactive state transitions without UI

## Test Categories

### QueryInvalidationService Tests
Test cache invalidation logic:
- **Mapping**: Verify domain events map to correct query keys
- **Multiple Keys**: Test events that invalidate multiple queries
- **Unknown Events**: Verify graceful handling of unmapped events
- **Example**: See `QueryInvalidationServiceTests.cs`

### OptimisticUpdateService Tests
Test optimistic update logic:
- **Add/Remove**: Test adding and removing optimistic items
- **Expiration**: Verify items are removed after confirmation
- **Threading**: Test concurrent add/remove operations
- **State Consistency**: Ensure state remains valid

### ReactiveQuery Tests (if applicable)
Test query state management:
- **Loading States**: Test transitions (Idle → Loading → Success/Error)
- **Retry Logic**: Verify retry behavior on failure
- **Invalidation**: Test query invalidation triggers refetch
- **Optimistic Updates**: Test mutation integration

## Writing New Tests
1. **Create Test Class**: Follow pattern `{ServiceName}Tests.cs`
2. **Mock API Clients**: Use mocking library (e.g., NSubstitute) for `IBooksClient`
3. **Arrange-Act-Assert**: Follow AAA pattern
4. **Test Scenarios**: Focus on business logic, not UI rendering
5. **Edge Cases**: Test error handling, null cases, concurrent operations

## Example Test Structure
```csharp
public class QueryInvalidationServiceTests
{
    [Test]
    public void BookCreated_InvalidatesBookQueries()
    {
        // Arrange
        var service = new QueryInvalidationService();
        var notification = new BookCreatedNotification(Guid.NewGuid());
        
        // Act
        var keys = service.GetInvalidationKeys(notification);
        
        // Assert
        await Assert.That(keys).Contains("Books");
    }
}
```

## Mocking Example
```csharp
// Mock API client
var mockClient = Substitute.For<IBooksClient>();
mockClient.GetBooksAsync(Arg.Any<int>(), Arg.Any<int>())
    .Returns(Task.FromResult(new PagedListDto<BookDto>()));

// Use in test
var service = new MyService(mockClient);
```

## Running Tests
- **All web tests**: `dotnet test tests/BookStore.Web.Tests/`
- **Specific test**: `dotnet test --filter "FullyQualifiedName~QueryInvalidationServiceTests"`

## Key Test Files
- [QueryInvalidationServiceTests.cs](file:///Users/antaoalmada/Projects/BookStore/tests/BookStore.Web.Tests/QueryInvalidationServiceTests.cs) - Example of service testing
- [WebTests.cs](file:///Users/antaoalmada/Projects/BookStore/tests/BookStore.Web.Tests/WebTests.cs) - Example of integration testing

## Note on Component Testing
For Blazor component testing (UI rendering), consider using **bUnit** library. These tests focus on service logic only.
