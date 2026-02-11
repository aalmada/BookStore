# Web Frontend Unit Tests Instructions

**Scope**: `tests/BookStore.Web.Tests/**`

## Guides
- `docs/guides/testing-guide.md` - Testing patterns
- `docs/guides/real-time-notifications.md` - SSE & cache invalidation

## Skills
- `/run-unit-tests` - Run unit tests

## Rules
- **TUnit only** (not xUnit/NUnit) - Use `[Test]` and `await Assert.That(...)`
- **Avoid `Task.Delay`** - Use mocks and direct execution
- Test `QueryInvalidationService`, `OptimisticUpdateService` in isolation
- Mock API clients (`IBooksClient`, `IAuthorsClient`)
- Naming: `{ServiceName}Tests.cs`
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
