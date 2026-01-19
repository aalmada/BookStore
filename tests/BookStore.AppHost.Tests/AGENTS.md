# AppHost Integration Tests Instructions

**Scope**: `tests/BookStore.AppHost.Tests/**`

## Purpose
End-to-end integration tests using Aspire's testing framework. Tests the full application stack: API, database,authentication, SSE notifications, and business logic.

## Core Patterns
- **Test Framework**: **MUST use TUnit** for all tests (not xUnit or NUnit).
- **Assertions**: Use TUnit assertions: `await Assert.That(...).IsTrue()`, `.IsFalse()`, `.IsEqualTo()`, `.IsNotNull()`, etc.
- **Aspire Testing**: Uses `DistributedApplicationTestingBuilder` to spin up the entire app stack.
- **Global Setup**: `GlobalSetup.cs` initializes the app once per test session and authenticates an admin user.
- **SSE Testing**: `TestHelpers.ExecuteAndWaitForEventAsync()` verifies that mutations trigger SSE notifications.
- **Fake Data**: Uses `Bogus` library via `TestHelpers.GenerateFake*Request()` methods.
- **Data Isolation**: Tests **MUST NOT rely on background seeding**. Use `GlobalSetup` or `[Before(Test)]` hooks to manually seed required data (Tenants, Users, Books) using helper methods.
- **Consistency**: When creating data, ensure availability in projections by using waiting helpers (e.g., `WaitForConditionAsync`) before assertions.

## Test Structure
- **Naming**: `{Feature}Tests.cs` (e.g., `BookCrudTests.cs`, `AuthorCrudTests.cs`)
- **Assertions**: **Always use TUnit assertions**: `await Assert.That(actual).IsEqualTo(expected)`, `await Assert.That(condition).IsTrue()`, etc.
  - `TestHelpers.GetAuthenticatedClientAsync()` for admin operations
  - `TestHelpers.GetUnauthenticatedClient()` for public API tests
- **Multi-Tenancy**: Use `client.DefaultRequestHeaders.Add("X-Tenant-ID", "tenant-id")` to test isolation.

## Writing New Tests
1. **Create Test Class**: Follow naming pattern `{Feature}Tests.cs`
2. **Use Test Helpers**: Leverage existing helpers in `TestHelpers.cs` for common operations (CreateBookAsync, AddToCartAsync, etc.)
3. **Wait for SSE Events**: Use `ExecuteAndWaitForEventAsync()` to ensure mutations complete and broadcast events
4. **Self-Contained Data**: Create all necessary prerequisite data within the test or setup (e.g., if you need a book, create it first). Do not assume "default" data exists.
5. **Cleanup**: Tests should be idempotent; use fresh data for each test

## Common Helpers
- `CreateBookAsync()` - Creates a book and waits for `BookUpdated` event
- `DeleteBookAsync()` / `RestoreBookAsync()` - Soft delete operations with event waiting
- `AddToCartAsync()` / `RemoveFromCartAsync()` - Cart operations with `UserUpdated` event
- `RateBookAsync()` / `AddToFavoritesAsync()` - User interactions with event confirmation

## Running Tests
- **All integration tests**: `dotnet test tests/BookStore.AppHost.Tests/BookStore.AppHost.Tests.csproj`
- **Specific test**: `dotnet test --filter "FullyQualifiedName~BookCrudTests"`

## Key Test Files
- [GlobalSetup.cs](file:///Users/antaoalmada/Projects/BookStore/tests/BookStore.AppHost.Tests/GlobalSetup.cs) - Test session setup and admin authentication
- [TestHelpers.cs](file:///Users/antaoalmada/Projects/BookStore/tests/BookStore.AppHost.Tests/TestHelpers.cs) - Reusable test utilities and SSE helpers
- [TestConstants.cs](file:///Users/antaoalmada/Projects/BookStore/tests/BookStore.AppHost.Tests/TestConstants.cs) - Shared constants and timeouts
