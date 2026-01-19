---
name: scaffold-test
description: Create integration tests for API endpoints with SSE event verification and TUnit patterns. Use this when you need to test a new endpoint.
license: MIT
---

Follow this guide to create **integration tests** for API endpoints in `tests/BookStore.AppHost.Tests`.

1. **Create Test Class**
   - Create file in `tests/BookStore.AppHost.Tests/Tests/`
   - **Naming**: `{Feature}Tests.cs` (e.g., `AuthorCrudTests.cs`)
   - **Template**:
     ```csharp
     using TUnit.Core;
     using TUnit.Assertions.Extensions;
     using BookStore.Shared.Models;
     
     namespace BookStore.AppHost.Tests.Tests;
     
     public class AuthorCrudTests
     {
         // Test methods here
     }
     ```

2. **Write Create Test (with SSE)**
   - Test endpoint that creates a resource
   - **Use TestHelpers** for SSE event verification
   - **Example**:
     ```csharp
     [Test]
     public async Task CreateAuthor_ValidRequest_CreatesAndNotifies()
     {
         // Arrange
         var client = await TestHelpers.GetAuthenticatedClientAsync();
         var request = TestHelpers.GenerateFakeAuthorRequest();
         
         // Act - ExecuteAndWaitForEventAsync automatically:
         // 1. Makes the HTTP request
         // 2. Waits for SSE notification
         // 3. Returns the created resource
         var (author, _) = await TestHelpers.ExecuteAndWaitForEventAsync<AuthorDto>(
             client,
             async () => await client.PostAsJsonAsync("/api/admin/authors", request),
             "AuthorUpdated",  // Wait for this SSE event
             timeout: TimeSpan.FromSeconds(10)
         );
         
         // Assert
         await Assert.That(author).IsNotNull();
         await Assert.That(author!.Name).IsEqualTo(request.Name);
         await Assert.That(author.Biography).IsEqualTo(request.Biography);
     }
     ```

3. **Write Update Test**
   - Test endpoint that updates a resource
   - **Pattern**: Create → Update → Verify
     ```csharp
     [Test]
     public async Task UpdateAuthor_ValidRequest_UpdatesAndNotifies()
     {
         // Arrange
         var client = await TestHelpers.GetAuthenticatedClientAsync();
         var createRequest = TestHelpers.GenerateFakeAuthorRequest();
         
         // Create author first
         var (created, _) = await TestHelpers.ExecuteAndWaitForEventAsync<AuthorDto>(
             client,
             async () => await client.PostAsJsonAsync("/api/admin/authors", createRequest),
             "AuthorUpdated"
         );
         
         var updateRequest = new UpdateAuthorRequest(
             Name: "Updated Name",
             Biography: "Updated Biography"
         );
         
         // Act
         var (updated, _) = await TestHelpers.ExecuteAndWaitForEventAsync<AuthorDto>(
             client,
             async () => await client.PutAsJsonAsync($"/api/admin/authors/{created!.Id}", updateRequest),
             "AuthorUpdated"
         );
         
         // Assert
         await Assert.That(updated).IsNotNull();
         await Assert.That(updated!.Name).IsEqualTo("Updated Name");
         await Assert.That(updated.Biography).IsEqualTo("Updated Biography");
         await Assert.That(updated.Id).IsEqualTo(created.Id);  // Same ID
     }
     ```

4. **Write Delete Test (Soft Delete)**
   - Test soft deletion with restore capability
     ```csharp
     [Test]
     public async Task DeleteAuthor_ExistingAuthor_SoftDeletes()
     {
         // Arrange
         var client = await TestHelpers.GetAuthenticatedClientAsync();
         var request = TestHelpers.GenerateFakeAuthorRequest();
         
         var (created, _) = await TestHelpers.ExecuteAndWaitForEventAsync<AuthorDto>(
             client,
             async () => await client.PostAsJsonAsync("/api/admin/authors", request),
             "AuthorUpdated"
         );
         
         // Act - Delete
         var deleteResponse = await client.DeleteAsync($"/api/admin/authors/{created!.Id}");
         await Assert.That(deleteResponse.IsSuccessStatusCode).IsTrue();
         
         // Verify not in public list
         var listResponse = await TestHelpers.GetUnauthenticatedClient()
             .GetFromJsonAsync<PagedListDto<AuthorDto>>("/api/authors");
         
         await Assert.That(listResponse).IsNotNull();
         await Assert.That(listResponse!.Items.Any(a => a.Id == created.Id)).IsFalse();
     }
     ```

5. **Write Query Tests**
   - Test GET endpoints without SSE
     ```csharp
     [Test]
     public async Task GetAuthors_ReturnsPagedList()
     {
         // Arrange
         var client = TestHelpers.GetUnauthenticatedClient();
         
         // Act
         var response = await client.GetFromJsonAsync<PagedListDto<AuthorDto>>(
             "/api/authors?page=1&pageSize=20"
         );
         
         // Assert
         await Assert.That(response).IsNotNull();
         await Assert.That(response!.Items).IsNotNull();
         await Assert.That(response.TotalCount).IsGreaterThanOrEqualTo(0);
     }
     
     [Test]
     public async Task GetAuthorById_ExistingId_ReturnsAuthor()
     {
         // Arrange - Create an author first
         var client = await TestHelpers.GetAuthenticatedClientAsync();
         var request = TestHelpers.GenerateFakeAuthorRequest();
         
         var (created, _) = await TestHelpers.ExecuteAndWaitForEventAsync<AuthorDto>(
             client,
             async () => await client.PostAsJsonAsync("/api/admin/authors", request),
             "AuthorUpdated"
         );
         
         // Act - Get by ID (public endpoint)
         var unauthClient = TestHelpers.GetUnauthenticatedClient();
         var author = await unauthClient.GetFromJsonAsync<AuthorDto>(
             $"/api/authors/{created!.Id}"
         );
         
         // Assert
         await Assert.That(author).IsNotNull();
         await Assert.That(author!.Id).IsEqualTo(created.Id);
         await Assert.That(author.Name).IsEqualTo(request.Name);
     }
     ```

6. **Add Custom Test Helper (if needed)**
   - For resource-specific operations, add to `TestHelpers.cs`:
     ```csharp
     public static async Task<AuthorDto> CreateAuthorAsync(
         HttpClient client,
         CreateAuthorRequest? request = null)
     {
         request ??= GenerateFakeAuthorRequest();
         
         var (author, _) = await ExecuteAndWaitForEventAsync<AuthorDto>(
             client,
             async () => await client.PostAsJsonAsync("/api/admin/authors", request),
             "AuthorUpdated"
         );
         
         return author!;
     }
     
     public static CreateAuthorRequest GenerateFakeAuthorRequest()
     {
         var faker = new Faker();
         return new CreateAuthorRequest(
             Name: faker.Name.FullName(),
             Biography: faker.Lorem.Paragraph()
         );
     }
     ```

7. **Test Error Cases**
   - Test validation failures and edge cases
     ```csharp
     [Test]
     public async Task CreateAuthor_EmptyName_ReturnsBadRequest()
     {
         // Arrange
         var client = await TestHelpers.GetAuthenticatedClientAsync();
         var request = new CreateAuthorRequest(Name: "", Biography: "Bio");
         
         // Act
         var response = await client.PostAsJsonAsync("/api/admin/authors", request);
         
         // Assert
         await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
     }
     
     [Test]
     public async Task DeleteAuthor_Unauthenticated_ReturnsUnauthorized()
     {
         // Arrange
         var client = TestHelpers.GetUnauthenticatedClient();
         var authorId = Guid.CreateVersion7();
         
         // Act
         var response = await client.DeleteAsync($"/api/admin/authors/{authorId}");
         
         // Assert
         await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
     }
     ```

## Key Testing Patterns

### Use Authenticated Client for Admin Endpoints
```csharp
var client = await TestHelpers.GetAuthenticatedClientAsync();
```

### Use Unauthenticated Client for Public Endpoints
```csharp
var client = TestHelpers.GetUnauthenticatedClient();
```

### Multi-Tenancy
```csharp
// Manual tenant isolation testing
var client = await TestHelpers.GetAuthenticatedClientAsync();
client.DefaultRequestHeaders.Add("X-Tenant-ID", "acme");
```

### Wait for SSE Events After Mutations
```csharp
var (result, notification) = await TestHelpers.ExecuteAndWaitForEventAsync<T>(
    client,
    async () => /* HTTP call */,
    "EventName"
);
```

### Use Bogus for Fake Data
```csharp
var faker = new Faker();
var name = faker.Name.FullName();
var email = faker.Internet.Email();
```

## TUnit Assertion Patterns

```csharp
// Equality
await Assert.That(actual).IsEqualTo(expected);

// Null checks
await Assert.That(value).IsNotNull();
await Assert.That(value).IsNull();

// Boolean
await Assert.That(condition).IsTrue();
await Assert.That(condition).IsFalse();

// Collections
await Assert.That(collection).Contains(item);
await Assert.That(collection).DoesNotContain(item);

// Numeric comparisons
await Assert.That(count).IsGreaterThan(0);
await Assert.That(count).IsGreaterThanOrEqualTo(0);

// Exceptions
await Assert.That(() => action()).Throws<InvalidOperationException>();
```

## Running Tests

Once tests are created, use the dedicated test runner skills:

- **`/run-integration-tests`** - Execute all integration tests with Aspire
- **`/run-unit-tests`** - Execute unit tests for API and analyzers  
- **`/verify-feature`** - Complete verification (build + format + all tests)

For specific test filtering or manual commands, see:
- [run-integration-tests](../run-integration-tests/SKILL.md) - Integration test details
- [run-unit-tests](../run-unit-tests/SKILL.md) - Unit test details

### Quick Reference

```bash
# All integration tests
/run-integration-tests

# Specific test class
dotnet test --filter "FullyQualifiedName~AuthorCrudTests"

# Complete verification
/verify-feature
```

## Troubleshooting

**Test Hangs on SSE Wait**
- Check event name matches exactly (case-sensitive)
- Verify `MartenCommitListener` sends the notification
- Increase timeout if needed

**Port Already in Use**
- Stop any running Aspire instances
- Check for orphaned `dotnet` processes

**"Zero tests ran"**
- Ensure test class is public
- Ensure methods are decorated with `[Test]`
- Check GlobalSetup completed successfully

## Related Skills

**Prerequisites**:
- Feature must be implemented first - see scaffolding skills:
  - `/scaffold-write` - Backend mutations
  - `/scaffold-read` - Backend queries
  - `/scaffold-frontend-feature` - UI components

**Next Steps**:
- `/run-integration-tests` - Execute the tests you created
- `/verify-feature` - Complete verification workflow

**See Also**:
- [verify-feature](../verify-feature/SKILL.md) - Definition of Done verification
- AppHost.Tests AGENTS.md - Test project patterns

## Next Steps

After creating tests, run:
1. **Verify**: `/verify-feature` to ensure all tests pass
2. **Check Coverage**: Review which scenarios are tested
3. **Add Edge Cases**: Test boundary conditions and error scenarios
