# Integration Testing Guide

This guide explains the integration testing strategy for the BookStore application using .NET Aspire and TUnit.

## Overview

The BookStore project uses comprehensive integration tests to verify end-to-end functionality across the entire application stack, including the API, database, and infrastructure components.

## Test Organization

Integration tests are located in `tests/BookStore.AppHost.Tests/` and organized by domain:

```
BookStore.AppHost.Tests/
├── BookCrudTests.cs           # Book CRUD operations
├── AuthorCrudTests.cs         # Author CRUD operations
├── CategoryCrudTests.cs       # Category CRUD operations
├── PublisherCrudTests.cs      # Publisher CRUD operations
├── ErrorScenarioTests.cs      # Error handling & validation
├── PublicApiTests.cs          # Public (unauthenticated) endpoints
├── WebTests.cs                # Web resource health checks
├── FrontendTests.cs           # Frontend health checks
├── InfrastructureTests.cs     # Infrastructure resource health
├── DatabaseTests.cs           # Database connectivity
├── TestHelpers.cs             # Shared authentication helpers
├── TestDataGenerators.cs      # Bogus-based fake data generators
├── TestConstants.cs           # Shared test constants
└── GlobalSetup.cs             # Global test setup and teardown
```

## Key Features

### 1. Shared Authentication Token

To avoid circuit breaker issues from concurrent authentication requests, tests use a shared authentication token:

```csharp
// GlobalSetup.cs authenticates once during test session setup
public static string? AdminAccessToken { get; private set; }

// Tests reuse the shared token
var httpClient = await TestHelpers.GetAuthenticatedClientAsync();
```

**Benefits:**
- ✅ Eliminates circuit breaker trips from parallel authentication
- ✅ Faster test execution (authenticate once, not per test)
- ✅ Tests can run in parallel without conflicts

### 2. Realistic Test Data with Bogus

All test data is generated using the [Bogus](https://github.com/bchavez/Bogus) library for realistic, varied data:

```csharp
// TestDataGenerators.cs
public static object GenerateFakeBookRequest() => new
{
    Title = _faker.Commerce.ProductName(),      // "Incredible Concrete Chips"
    Isbn = _faker.Commerce.Ean13(),             // "1234567890123"
    Language = "en",
    Translations = new Dictionary<string, object>
    {
        ["en"] = new { Description = _faker.Lorem.Paragraph() }
    },
    PublicationDate = new
    {
        Year = _faker.Date.Past(10).Year,
        Month = _faker.Random.Int(1, 12),
        Day = _faker.Random.Int(1, 28)
    }
};
```

**Benefits:**
- ✅ Unique data on every test run (no conflicts)
- ✅ Realistic data catches edge cases
- ✅ No hardcoded test data to maintain

### 3. Test Isolation

Each test creates its own `HttpClient` to avoid concurrency issues:

```csharp
public static async Task<HttpClient> GetAuthenticatedClientAsync()
{
    var httpClient = app.CreateHttpClient("apiservice");
    httpClient.DefaultRequestHeaders.Authorization = 
        new AuthenticationHeaderValue("Bearer", GlobalHooks.AdminAccessToken);
    return httpClient;
}
```

### 4. Resilience Handler Disabled for Tests

The standard resilience handler (circuit breaker, retry policies) is disabled in the test environment to prevent false failures during parallel test execution:

```csharp
// GlobalSetup.cs
// Disable resilience handler for tests to avoid circuit breaker issues
// builder.Services.ConfigureHttpClientDefaults(clientBuilder =>
// {
//     clientBuilder.AddStandardResilienceHandler();
// });
```

## Writing Integration Tests

### Basic CRUD Test Pattern

```csharp
[Test]
public async Task CreateBook_EndToEndFlow_ShouldReturnOk()
{
    // Arrange
    var httpClient = await TestHelpers.GetAuthenticatedClientAsync();
    var createBookRequest = TestDataGenerators.GenerateFakeBookRequest();

    // Act
    var createResponse = await httpClient.PostAsJsonAsync("/api/admin/books", createBookRequest);

    // Assert
    _ = await Assert.That(createResponse.IsSuccessStatusCode).IsTrue();
}
```

### Error Scenario Test Pattern

```csharp
[Test]
public async Task CreateBook_WithoutAuth_ShouldReturnUnauthorized()
{
    // Arrange
    var httpClient = TestHelpers.GetUnauthenticatedClient();
    var createBookRequest = TestDataGenerators.GenerateFakeBookRequest();

    // Act
    var createResponse = await httpClient.PostAsJsonAsync("/api/admin/books", createBookRequest);

    // Assert
    _ = await Assert.That(createResponse.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
}
```

### Public API Test Pattern

```csharp
[Test]
public async Task GetBooks_PublicEndpoint_ShouldReturnOk()
{
    // Arrange
    var httpClient = TestHelpers.GetUnauthenticatedClient();

    // Act
    var response = await httpClient.GetAsync("/api/books");

    // Assert
    _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
}
```

## Server-Sent Events (SSE) Testing

The application sends real-time notifications via Server-Sent Events when mutations occur. Integration tests verify that SSE events are properly triggered for all CRUD operations.

### SSE Infrastructure

The test helper `ExecuteAndWaitForEventAsync` connects to the SSE stream BEFORE executing mutations, ensuring no race conditions:

```csharp
// TestHelpers.cs
public static async Task<bool> ExecuteAndWaitForEventAsync(
    Guid entityId,
    string eventType,
    Func<Task> action,
    TimeSpan timeout)
{
    // 1. Connect to SSE stream
    // 2. Wait for connection to establish
    // 3. Execute action (mutation)
    // 4. Wait for matching SSE event
    // 5. Return true if event received, false if timeout
}
```

**Event Types:**
- `CategoryCreated`, `CategoryUpdated`, `CategoryDeleted`
- `AuthorCreated`, `AuthorUpdated`, `AuthorDeleted`
- `PublisherCreated`, `PublisherUpdated`, `PublisherDeleted`
- `BookCreated`, `BookUpdated`, `BookDeleted`

> [!NOTE]
> Creation events often appear as "Updated" due to projection upsert semantics. Restore operations appear as "Updated" events (IsDeleted changes from true → false).

### SSE Test Pattern

```csharp
[Test]
public async Task UpdateCategory_ShouldReturnOk()
{
    // Arrange
    var httpClient = await TestHelpers.GetAuthenticatedClientAsync();
    dynamic createRequest = TestDataGenerators.GenerateFakeCategoryRequest();
    var createResponse = await httpClient.PostAsJsonAsync("/api/admin/categories", (object)createRequest);
    _ = await Assert.That(createResponse.StatusCode).IsEqualTo(HttpStatusCode.Created);
    var createdCategory = await createResponse.Content.ReadFromJsonAsync<CategoryDto>();

    dynamic updateRequest = TestDataGenerators.GenerateFakeCategoryRequest(); // New data

    // Act - Connect to SSE before updating, then wait for notification
    var received = await TestHelpers.ExecuteAndWaitForEventAsync(
        createdCategory!.Id,
        "CategoryUpdated",
        async () =>
        {
            var updateResponse = await httpClient.PutAsJsonAsync($"/api/admin/categories/{createdCategory.Id}", (object)updateRequest);
            _ = await Assert.That(updateResponse.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        },
        TimeSpan.FromSeconds(10));

    // Assert
    _ = await Assert.That(received).IsTrue();
}
```

### Delete with SSE Verification

```csharp
[Test]
public async Task DeletePublisher_ShouldReturnNoContent()
{
    // Arrange
    var httpClient = await TestHelpers.GetAuthenticatedClientAsync();
    dynamic createRequest = TestDataGenerators.GenerateFakePublisherRequest();
    var createResponse = await httpClient.PostAsJsonAsync("/api/admin/publishers", (object)createRequest);
    var createdPublisher = await createResponse.Content.ReadFromJsonAsync<PublisherDto>();

    // Act - Connect to SSE before deleting
    var received = await TestHelpers.ExecuteAndWaitForEventAsync(
        createdPublisher!.Id,
        "PublisherDeleted",
        async () =>
        {
            var deleteResponse = await httpClient.DeleteAsync($"/api/admin/publishers/{createdPublisher.Id}");
            _ = await Assert.That(deleteResponse.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        },
        TimeSpan.FromSeconds(10));

    _ = await Assert.That(received).IsTrue();
}
```

### Restore with SSE Verification

```csharp
[Test]
public async Task RestoreAuthor_ShouldReturnOk()
{
    // Arrange
    var httpClient = await TestHelpers.GetAuthenticatedClientAsync();
    var createRequest = TestDataGenerators.GenerateFakeAuthorRequest();
    var createResponse = await httpClient.PostAsJsonAsync("/api/admin/authors", createRequest);
    var createdAuthor = await createResponse.Content.ReadFromJsonAsync<AuthorDto>();

    // Soft delete first
    var deleteResponse = await httpClient.DeleteAsync($"/api/admin/authors/{createdAuthor!.Id}");
    _ = await Assert.That(deleteResponse.StatusCode).IsEqualTo(HttpStatusCode.NoContent);

    // Act - Restore appears as "Updated" event (IsDeleted: true → false)
    var received = await TestHelpers.ExecuteAndWaitForEventAsync(
        createdAuthor.Id,
        "AuthorUpdated",
        async () =>
        {
            var restoreResponse = await httpClient.PostAsync($"/api/admin/authors/{createdAuthor.Id}/restore", null);
            _ = await Assert.That(restoreResponse.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        },
        TimeSpan.FromSeconds(10));

    _ = await Assert.That(received).IsTrue();
}
```

### Matching Any Entity ID

Use `Guid.Empty` to match any entity ID (useful for creation tests where the ID is unknown):

```csharp
var received = await TestHelpers.ExecuteAndWaitForEventAsync(
    Guid.Empty, // Match any ID
    "BookUpdated",
    async () =>
    {
        var createResponse = await httpClient.PostAsJsonAsync("/api/admin/books", createBookRequest);
        createdBook = await createResponse.Content.ReadFromJsonAsync<BookResponse>();
    },
    TimeSpan.FromSeconds(10));
```

### Test Isolation with [NotInParallel]

SSE tests should use the `[NotInParallel]` attribute to prevent connection race conditions:

```csharp
[NotInParallel]
public class CategoryCrudTests
{
    // Tests run sequentially to avoid SSE connection conflicts
}
```

## Running Tests

### Run All Tests

```bash
dotnet test
```

### Run Specific Test File

```bash
dotnet test --filter "FullyQualifiedName~BookCrudTests"
```

### Run with Detailed Output

```bash
dotnet test --logger "console;verbosity=detailed"
```

## Test Configuration

### Default Timeout

All tests use a shared timeout constant:

```csharp
// TestConstants.cs
public static class TestConstants
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);
}
```

### Global Setup

The `GlobalSetup.cs` file handles:
- ✅ Starting the Aspire application
- ✅ Authenticating as admin user
- ✅ Sharing authentication token across tests
- ✅ Configuring logging
- ✅ Cleaning up resources after tests

## Best Practices

### 1. Use Test Helpers

Always use the provided helper methods instead of duplicating code:

```csharp
// ✅ Good
var httpClient = await TestHelpers.GetAuthenticatedClientAsync();

// ❌ Bad - duplicates authentication logic
var httpClient = app.CreateHttpClient("apiservice");
// ... manual authentication code ...
```

### 2. Use Test Data Generators

Always use Bogus generators for test data:

```csharp
// ✅ Good
var request = TestDataGenerators.GenerateFakeBookRequest();

// ❌ Bad - hardcoded data can cause conflicts
var request = new { Title = "Test Book", Isbn = "1234567890123" };
```

### 3. Discard Unused Assert Results

Follow the project's coding style by discarding unused assertion results:

```csharp
// ✅ Good
_ = await Assert.That(response.IsSuccessStatusCode).IsTrue();

// ❌ Bad - IDE0058 warning
await Assert.That(response.IsSuccessStatusCode).IsTrue();
```

### 4. Test One Thing

Each test should verify a single behavior:

```csharp
// ✅ Good - tests one thing
[Test]
public async Task CreateBook_ShouldReturnOk() { /* ... */ }

[Test]
public async Task UpdateBook_ShouldReturnOk() { /* ... */ }

// ❌ Bad - tests multiple things
[Test]
public async Task BookCrudOperations_ShouldWork() 
{
    // Creates, updates, deletes - too much!
}
```

### 5. Use Descriptive Test Names

Test names should clearly describe what is being tested:

```csharp
// ✅ Good - clear what's being tested
CreateBook_WithInvalidData_ShouldReturnBadRequest

// ❌ Bad - unclear
TestBookCreation
```

## Troubleshooting

### Circuit Breaker Issues

If you see `BrokenCircuitException` errors:
- Ensure the resilience handler is disabled in `GlobalSetup.cs`
- Verify tests are using the shared authentication token
- Check that each test creates its own `HttpClient`

### Authentication Failures

If tests fail with 401 Unauthorized:
- Verify `GlobalSetup.cs` successfully authenticated
- Check that `GlobalHooks.AdminAccessToken` is not null
- Ensure the admin user exists in the seeded data

### Database Conflicts

If tests fail with unique constraint violations:
- Verify you're using `TestDataGenerators` for all test data
- Ensure ISBNs are unique (Bogus generates unique values)
- Check that tests aren't hardcoding IDs or other unique values

## Code Coverage

To enable code coverage with TUnit:

```bash
dotnet test --collect:"XPlat Code Coverage"
```

Coverage reports will be generated in the `TestResults` directory.

## Additional Resources

- [TUnit Documentation](https://github.com/thomhurst/TUnit)
- [.NET Aspire Testing](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/testing)
- [Bogus Documentation](https://github.com/bchavez/Bogus)
