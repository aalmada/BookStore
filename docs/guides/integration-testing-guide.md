# Integration Testing Guide

This guide explains the integration testing strategy for the BookStore application using .NET Aspire and TUnit.

## Overview

The BookStore project uses comprehensive integration tests to verify end-to-end functionality across the entire application stack, including the API, database, caching, storage, and identity infrastructure.

## Test Organization

Integration tests are located in `tests/BookStore.AppHost.Tests/` and organized by domain:

```
BookStore.AppHost.Tests/
├── Helpers/                        # Shared test helpers
│   ├── AuthenticationHelpers.cs    # Login / user-creation helpers
│   ├── AuthorHelpers.cs            # Author CRUD + SSE helpers
│   ├── BookHelpers.cs              # Book CRUD + SSE helpers
│   ├── CategoryHelpers.cs          # Category CRUD + SSE helpers
│   ├── DatabaseHelpers.cs          # Direct Marten access + tenant seeding
│   ├── FakeDataGenerators.cs       # Bogus-based fake data generators
│   ├── HttpClientHelpers.cs        # HTTP / Refit client factory
│   ├── PublisherHelpers.cs         # Publisher CRUD + SSE helpers
│   ├── ShoppingCartHelpers.cs      # Shopping cart helpers
│   ├── SseEventHelpers.cs          # SSE subscription + condition polling
│   └── WebAuthnTestHelper.cs       # Passkey / WebAuthn helpers
├── Data/
│   └── HttpClientDataClass.cs      # TUnit data-driven class helper
├── Services/
│   └── BlobStorageTests.cs         # Blob storage service tests
├── AccountIsolationTests.cs        # Account isolation across tenants
├── AccountLockoutTests.cs          # Account lockout security
├── AdminTenantTests.cs             # Tenant administration
├── AdminUserTests.cs               # Admin user management
├── ApiDocumentationTests.cs        # OpenAPI / Scalar documentation
├── AuthTests.cs                    # Register / Login / token flows
├── AuthorCrudTests.cs              # Author CRUD operations
├── BookConcurrencyTests.cs         # ETag-based optimistic concurrency
├── BookCrudTests.cs                # Book CRUD operations
├── BookFilterRegressionTests.cs    # Book search / filter regression
├── BookRatingTests.cs              # Book rating feature
├── BookSoftDeleteTests.cs          # Book soft-delete and restore
├── BookValidationTests.cs          # Book validation rules
├── CategoryConcurrencyTests.cs     # Category optimistic concurrency
├── CategoryCrudTests.cs            # Category CRUD operations
├── CategoryOrderingTests.cs        # Category ordering
├── ConcurrencyTests.cs             # General concurrency scenarios
├── ConfigurationEndpointsTests.cs  # Configuration endpoint health
├── CorrelationTests.cs             # Correlation-ID propagation
├── CorsTests.cs                    # CORS policy tests
├── CrossTenantAuthenticationTests.cs # Cross-tenant auth boundaries
├── DatabaseTests.cs                # Database connectivity
├── EmailVerificationTests.cs       # Email verification flow
├── ErrorScenarioTests.cs           # Error handling & validation
├── FavoriteBooksTests.cs           # Favourite-books feature
├── FrontendTests.cs                # Frontend resource health
├── InfrastructureTests.cs          # Infrastructure resource health
├── LocalizationTests.cs            # Localisation / Accept-Language
├── ManagementIntegrationTests.cs   # Management API integration
├── MultiLanguageTranslationTests.cs # Multi-language content
├── MultiTenancyTests.cs            # Multi-tenancy data isolation
├── MultiTenantAuthenticationTests.cs # Multi-tenant auth scenarios
├── PasskeyDeletionTests.cs         # Passkey deletion
├── PasskeyRegistrationSecurityTests.cs # Passkey registration security
├── PasskeySecurityTests.cs         # Passkey general security
├── PasskeyTenantIsolationTests.cs  # Passkey tenant isolation
├── PasskeyTestHelpers.cs           # Passkey-specific test utilities
├── PasskeyTests.cs                 # Passkey login flow
├── PasswordGeneratorTests.cs       # Password generation
├── PasswordManagementTests.cs      # Password change / reset
├── PriceFilterRegressionTests.cs   # Price filter regression
├── PublicApiTests.cs               # Public (unauthenticated) endpoints
├── PublisherCrudTests.cs           # Publisher CRUD operations
├── RateLimitTests.cs               # Rate limiting behaviour
├── RefitMartenRegressionTests.cs   # Refit + Marten regression tests
├── RefreshTokenSecurityTests.cs    # Refresh token security
├── SearchTests.cs                  # Full-text search
├── SecurityHeadersTests.cs         # HTTP security headers
├── SecurityStampValidationTests.cs # Security stamp invalidation
├── ShoppingCartTests.cs            # Shopping cart feature
├── TenantInfoTests.cs              # Tenant info endpoint
├── TenantSecurityTests.cs          # Tenant security rules
├── TenantUserIsolationTests.cs     # User isolation between tenants
├── UnverifiedAccountCleanupTests.cs # Unverified account cleanup
├── UpdateTests.cs                  # Update scenarios
├── WebTests.cs                     # Web resource health checks
├── TestConstants.cs                # Shared timeout / retry constants
└── GlobalSetup.cs                  # Global test setup and teardown
```

## Test Infrastructure with .NET Aspire

The integration tests leverage **[Aspire.Hosting.Testing](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/testing)** to automatically start and orchestrate all application components, providing true end-to-end testing in an isolated environment.

### How Aspire Bootstraps the Application

`GlobalSetup.cs` uses `DistributedApplicationTestingBuilder` to create and start the entire stack defined in `src/BookStore.AppHost/`:

```csharp
// GlobalSetup.cs — Before(TestSession)
var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.BookStore_AppHost>([
    "--RateLimit:Disabled=true",
    "--Seeding:Enabled=false",        // Tests seed their own data
    "--Email:DeliveryMethod=None",    // Suppress real emails
    "--Jwt:ExpirationMinutes=240"     // Long-lived tokens for test session
]);

builder.Services.AddLogging(logging =>
{
    logging.SetMinimumLevel(LogLevel.Information);
    logging.AddSimpleConsole(options => { options.SingleLine = true; });
});

App = await builder.BuildAsync();
NotificationService = App.Services.GetRequiredService<ResourceNotificationService>();
await App.StartAsync();
```

This single call automatically provisions and starts:

| Component | Resource Type | Purpose |
|-----------|---------------|---------|
| **PostgreSQL** | Database | Core data storage (Marten) |
| **Azurite** | Blob Storage | Azure Storage emulator for book cover images |
| **Redis** | Cache | HybridCache distributed backing store |
| **ApiService** | .NET Project | RESTful API backend |
| **WebFrontend** | .NET Project | Blazor web application |

> [!IMPORTANT]
> Aspire automatically handles:
> - ✅ Resource lifecycle management (start, stop, cleanup)
> - ✅ Health checks and readiness verification
> - ✅ Service discovery and connection strings
> - ✅ Dependency ordering (API waits for database)
> - ✅ Container orchestration for infrastructure services

### Test Session Retry

The assembly is decorated with `[assembly: Retry(3)]` so flaky tests are retried up to three times before being marked as failed.

### Self-Contained Seeding

Because automatic background seeding is disabled (`--Seeding:Enabled=false`), `GlobalSetup` seeds the minimum required data directly via Marten before attempting authentication:

1. Creates the default tenant document.
2. Creates the default admin user (hashed password, `Admin` role).
3. Seeds a small number of books for search tests.

Non-default tenants are created on demand via `DatabaseHelpers.CreateTenantViaApiAsync()` inside individual test class setups.

### Accessing Resources in Tests

Tests reach the API through Aspire's `CreateHttpClient()`, which resolves the service endpoint automatically:

```csharp
// Low-level access (rarely used directly in tests)
var httpClient = App.CreateHttpClient("apiservice");

// Preferred: use HttpClientHelpers
var httpClient = await HttpClientHelpers.GetAuthenticatedClientAsync();

// Preferred: get a typed Refit client directly
var client = await HttpClientHelpers.GetAuthenticatedClientAsync<IBooksClient>();
```

### Resource Health Monitoring

`ResourceNotificationService` lets tests and `GlobalSetup` wait for infrastructure readiness:

```csharp
// InfrastructureTests.cs
[Test]
[Arguments("postgres")]
[Arguments("cache")]
[Arguments("blobs")]
public async Task ResourceIsHealthy(string resourceName)
{
    await GlobalHooks.NotificationService!
        .WaitForResourceHealthyAsync(resourceName, CancellationToken.None)
        .WaitAsync(TestConstants.DefaultTimeout);
}
```

`GlobalSetup` also waits for `apiservice` to become healthy before attempting authentication:

```csharp
using var healthCts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
await NotificationService.WaitForResourceHealthyAsync("apiservice", healthCts.Token);
```

## Authentication in Tests

### Custom Identity System

BookStore uses its own identity system. The API exposes `/account/login` which returns a JWT `AccessToken`. Tests authenticate against this endpoint directly — there is no external identity provider involved in the integration test environment.

### Shared Admin Token

To avoid circuit breaker issues from parallel authentication requests, `GlobalSetup` authenticates once and exposes a shared token:

```csharp
// Available after GlobalSetup.SetUp()
public static string? AdminAccessToken { get; private set; }
public static HttpClient? AdminHttpClient { get; private set; }
```

Individual tests consume this via `HttpClientHelpers` — they do not authenticate again.

### Per-Tenant Admin Login

For multi-tenant tests, `AuthenticationHelpers.LoginAsAdminAsync` logs in as the admin for a specific tenant:

```csharp
var loginResponse = await AuthenticationHelpers.LoginAsAdminAsync(tenantId);
// loginResponse.AccessToken contains the JWT for that tenant's admin
```

The convention is `admin@{tenantId}.com` / `Admin123!`. For the default tenant the alias `bookstore` is used, giving `admin@bookstore.com`.

### Regular User Creation

`AuthenticationHelpers.CreateUserAndGetClientAsync` registers a brand-new user and returns an authenticated client — useful for testing user-level restrictions:

```csharp
var userClient = await AuthenticationHelpers.CreateUserAndGetClientAsync(tenantId);
// userClient.Client  — authenticated HttpClient
// userClient.UserId  — Guid extracted from the JWT "sub" claim
```

A typed Refit overload is also available:

```csharp
var client = await AuthenticationHelpers.CreateUserAndGetClientAsync<IBooksClient>(tenantId);
```

## HTTP Client Helpers

All HTTP client creation goes through `HttpClientHelpers` (`Helpers/HttpClientHelpers.cs`):

| Method | Returns | Description |
|--------|---------|-------------|
| `GetAuthenticatedClientAsync()` | `HttpClient` | Admin token + default tenant header |
| `GetAuthenticatedClientAsync<T>()` | `T` (Refit) | Refit client with admin token + default tenant |
| `GetAuthenticatedClient(token, tenantId)` | `HttpClient` | Token + specified tenant header |
| `GetUnauthenticatedClient()` | `HttpClient` | Default tenant, no auth |
| `GetUnauthenticatedClient<T>()` | `T` (Refit) | Refit client, default tenant, no auth |
| `GetUnauthenticatedClientWithLanguage<T>(lang)` | `T` (Refit) | Refit client with `Accept-Language` header |
| `GetTenantClientAsync(tenantId, token)` | `HttpClient` | Token + specified tenant header |

### Refit Clients

The `BookStore.Client` project exposes typed Refit interfaces (`IBooksClient`, `ICategoriesClient`, `IAuthorsClient`, `IPublishersClient`, `ITenantsClient`, `IIdentityClient`, etc.). Always prefer these over raw `HttpClient`:

```csharp
// ✅ Preferred — typed, compile-time safe
var client = await HttpClientHelpers.GetAuthenticatedClientAsync<ICategoriesClient>();
var category = await client.GetCategoryAsync(id);

// ❌ Avoid — raw HTTP, fragile strings
var httpClient = await HttpClientHelpers.GetAuthenticatedClientAsync();
var response = await httpClient.GetAsync($"/api/categories/{id}");
```

For manual assembly from an existing `HttpClient`, use `RestService.For<T>`:

```csharp
var refitClient = RestService.For<IBooksClient>(myHttpClient);
```

## Fake Data Generators

All test data is generated via `FakeDataGenerators` (Bogus-backed, `Helpers/FakeDataGenerators.cs`):

| Method | Returns |
|--------|---------|
| `GenerateFakeBookRequest(...)` | `CreateBookRequest` |
| `GenerateFakeUpdateBookRequest(...)` | `UpdateBookRequest` |
| `GenerateFakeAuthorRequest()` | `CreateAuthorRequest` |
| `GenerateFakeCategoryRequest()` | `CreateCategoryRequest` |
| `GenerateFakePublisherRequest()` | `CreatePublisherRequest` |
| `GenerateFakePassword()` | `string` meeting password policy |
| `GenerateFakeEmail()` | `string` email address |
| `GenerateFakeTenantId()` | `string` URL-friendly tenant ID |

```csharp
// ✅ Good
var request = FakeDataGenerators.GenerateFakeBookRequest();

// ❌ Bad — hardcoded data causes conflicts and misses realistic edge cases
var request = new CreateBookRequest { Title = "Test Book", Isbn = "1234567890123" };
```

## Entity Helpers

Domain-specific helpers in `Helpers/*Helpers.cs` wrap Refit calls with SSE event verification so a single call covers both the command and the read-model consistency check:

```csharp
// CategoryHelpers.CreateCategoryAsync:
//   1. Subscribes to SSE before sending the request
//   2. Sends POST /api/admin/categories
//   3. Waits for CategoryCreated / CategoryUpdated SSE event
//   4. Returns the freshly-fetched CategoryDto
var category = await CategoryHelpers.CreateCategoryAsync(client, createRequest);

// BookHelpers.CreateBookAsync follows the same pattern
var book = await BookHelpers.CreateBookAsync(client, createRequest);
```

These helpers throw if the expected event is not received within `TestConstants.DefaultEventTimeout`.

**Available helpers:**

| Helper class | Operations |
|---|---|
| `CategoryHelpers` | `CreateCategoryAsync`, `UpdateCategoryAsync`, `DeleteCategoryAsync` |
| `AuthorHelpers` | `CreateAuthorAsync`, `UpdateAuthorAsync`, `DeleteAuthorAsync` |
| `BookHelpers` | `CreateBookAsync`, `UpdateBookAsync`, `DeleteBookAsync` |
| `PublisherHelpers` | `CreatePublisherAsync`, `UpdatePublisherAsync`, `DeletePublisherAsync` |
| `ShoppingCartHelpers` | Shopping-cart specific operations |

## Server-Sent Events (SSE) Testing

The application broadcasts real-time notifications via SSE when mutations occur. Tests verify consistency by listening for SSE events instead of using delays or polling.

### SseEventHelpers

All SSE interaction goes through `SseEventHelpers` (`Helpers/SseEventHelpers.cs`).

#### ExecuteAndWaitForEventAsync

Connects to the SSE stream **before** executing the action, guaranteeing no race conditions:

```csharp
var received = await SseEventHelpers.ExecuteAndWaitForEventAsync(
    entityId,           // Guid to match (Guid.Empty = match any)
    "CategoryUpdated",  // Event type to wait for
    async () =>
    {
        await client.UpdateCategoryAsync(id, request, etag);
    },
    TestConstants.DefaultEventTimeout);

_ = await Assert.That(received).IsTrue();
```

Multiple accepted event types:

```csharp
var received = await SseEventHelpers.ExecuteAndWaitForEventAsync(
    request.Id,
    ["CategoryCreated", "CategoryUpdated"], // Accept either
    async () => await client.CreateCategoryWithResponseAsync(request),
    TestConstants.DefaultEventTimeout);
```

#### ExecuteAndWaitForEventWithVersionAsync

Returns `EventResult(bool Success, long Version)` — useful when the caller needs the stream version (e.g., to supply as an ETag):

```csharp
var result = await SseEventHelpers.ExecuteAndWaitForEventWithVersionAsync(
    category.Id,
    "CategoryUpdated",
    async () => await client.UpdateCategoryAsync(id, request, etag),
    TestConstants.DefaultEventTimeout,
    minVersion: currentVersion + 1,     // Only accept events at or above this version
    minTimestamp: DateTimeOffset.UtcNow // Only accept events at or after this time
);

_ = await Assert.That(result.Success).IsTrue();
```

#### WaitForConditionAsync

Polls a condition until it becomes true or times out. Use this when there is no SSE event to listen to (e.g., eventual consistency in search projections):

```csharp
await SseEventHelpers.WaitForConditionAsync(async () =>
{
    var results = await publicClient.GetBooksAsync(new BookSearchRequest { Search = uniqueTitle });
    return results.Items.Any(b => b.Title == uniqueTitle);
}, TestConstants.DefaultEventTimeout, "Book was not found in search results");
```

> [!IMPORTANT]
> Never use `Task.Delay` or `Thread.Sleep` to wait for eventual consistency. Always use `SseEventHelpers.WaitForConditionAsync` or the entity helpers that already wrap `ExecuteAndWaitForEventAsync`.

### SSE Stream Endpoint

The notifications stream is at `GET /api/notifications/stream` and requires:
- `Authorization: Bearer <token>`
- `X-Tenant-ID: <tenantId>`

The SSE `HttpClient.Timeout` is set to `TestConstants.DefaultStreamTimeout` (5 minutes) to prevent Aspire's default short timeout from prematurely closing the stream.

### Event Types

| Domain | Event Types |
|--------|------------|
| Category | `CategoryCreated`, `CategoryUpdated`, `CategoryDeleted` |
| Author | `AuthorCreated`, `AuthorUpdated`, `AuthorDeleted` |
| Publisher | `PublisherCreated`, `PublisherUpdated`, `PublisherDeleted` |
| Book | `BookCreated`, `BookUpdated`, `BookDeleted` |

> [!NOTE]
> Creation events often arrive as `*Updated` due to Marten projection upsert semantics. Restore operations (IsDeleted: `true` → `false`) also appear as `*Updated`.

### Full SSE Test Example

```csharp
[Test]
public async Task UpdateCategory_ShouldReturnOk()
{
    // Arrange
    var client = await HttpClientHelpers.GetAuthenticatedClientAsync<ICategoriesClient>();
    var createRequest = FakeDataGenerators.GenerateFakeCategoryRequest();
    var createdCategory = await CategoryHelpers.CreateCategoryAsync(client, createRequest);
    var updateRequest = FakeDataGenerators.GenerateFakeUpdateCategoryRequest();

    // Act — entity helper handles SSE verification internally
    var updatedCategory = await CategoryHelpers.UpdateCategoryAsync(client, createdCategory, updateRequest);

    // Assert
    var expectedName = updateRequest.Translations["en"].Name;
    _ = await Assert.That(updatedCategory.Name).IsEqualTo(expectedName);
}
```

## Multi-Tenant Testing

### Creating Test Tenants

Tests that require cross-tenant isolation create their own tenants in a `[Before(Class)]` hook:

```csharp
static string _tenant1 = string.Empty;
static string _tenant2 = string.Empty;

[Before(Class)]
public static async Task ClassSetup()
{
    _tenant1 = FakeDataGenerators.GenerateFakeTenantId();
    _tenant2 = FakeDataGenerators.GenerateFakeTenantId();
    await DatabaseHelpers.CreateTenantViaApiAsync(_tenant1);
    await DatabaseHelpers.CreateTenantViaApiAsync(_tenant2);
}
```

`DatabaseHelpers.CreateTenantViaApiAsync` calls `POST /api/admin/tenants` using the global admin token. This also creates the tenant's admin user (`admin@{tenantId}.com` / `Admin123!`). The call is idempotent — conflict responses (400/409) are silently ignored.

### Logging in as a Tenant Admin

```csharp
var login = await AuthenticationHelpers.LoginAsAdminAsync(_tenant1);
var client = RestService.For<IBooksClient>(
    HttpClientHelpers.GetAuthenticatedClient(login!.AccessToken, _tenant1));
```

### Verifying Tenant Isolation

```csharp
[Test]
public async Task EntitiesAreIsolatedByTenant()
{
    // Create in tenant1
    var book = await BookHelpers.CreateBookAsync(tenant1Client, createRequest);

    // Visible in tenant1
    var found = await tenant1Client.GetBookAsync(book.Id);
    _ = await Assert.That(found).IsNotNull();

    // NOT visible in tenant2
    var ex = await Assert.That(async () => await tenant2Client.GetBookAsync(book.Id))
        .Throws<ApiException>();
    _ = await Assert.That(ex!.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
}
```

### Direct Database Access

`DatabaseHelpers.GetDocumentStoreAsync` returns a configured `IDocumentStore` for scenarios that require bypassing the API:

```csharp
// MUST use 'await using' to prevent connection pool leaks
await using var store = await DatabaseHelpers.GetDocumentStoreAsync();
await using var session = store.LightweightSession(tenantId);
var user = await session.Query<ApplicationUser>().FirstOrDefaultAsync(u => u.Email == email);
```

## TestConstants

All timeouts and retry counts live in `TestConstants.cs`:

| Constant | Value | Purpose |
|----------|-------|---------|
| `DefaultTimeout` | 30 s | General async operations |
| `DefaultEventTimeout` | 30 s | SSE event waiting |
| `DefaultStreamTimeout` | 5 min | SSE `HttpClient.Timeout` |
| `DefaultPollingInterval` | 50 ms | `WaitForConditionAsync` poll interval |
| `DefaultRetryDelay` | 100 ms | Delay between retries |
| `DefaultMaxRetries` | 10 | Max polling retries |
| `ShortRetryCount` | 5 | Quick operations |
| `LongRetryCount` | 20 | Slow operations |

## Running Tests

### Run All Integration Tests

```bash
dotnet test tests/BookStore.AppHost.Tests/
```

### Limit Parallelism (resource-constrained machines)

TUnit arguments must be passed after `--` to be forwarded as program arguments:

```bash
dotnet test tests/BookStore.AppHost.Tests/ -- --maximum-parallel-tests 4
```

### Filter by Test Name

```bash
dotnet test tests/BookStore.AppHost.Tests/ --filter "FullyQualifiedName~BookCrudTests"
```

### Filter by Category

```bash
dotnet test tests/BookStore.AppHost.Tests/ -- --treenode-filter "/*/*/*/*[Category=Integration]"
```

### Detailed Logging

```bash
dotnet test tests/BookStore.AppHost.Tests/ --logger "console;verbosity=detailed"
```

## Playwright Setup

Browser-based tests (e.g., WebAuthn / passkey flows) use **Microsoft.Playwright**. Browsers must be installed separately after the first build:

```bash
dotnet build tests/BookStore.AppHost.Tests/BookStore.AppHost.Tests.csproj
node tests/BookStore.AppHost.Tests/bin/Debug/net10.0/.playwright/package/index.js install chromium
```

> [!IMPORTANT]
> Re-run the install step after `dotnet clean` or switching build configurations (`Debug`/`Release`).

## Best Practices

### 1. Use Entity Helpers for CRUD + SSE

```csharp
// ✅ Good — SSE verification is built-in
var category = await CategoryHelpers.CreateCategoryAsync(client, request);

// ❌ Bad — manually calling the API without waiting for read-model consistency
var response = await httpClient.PostAsJsonAsync("/api/admin/categories", request);
```

### 2. Use Refit Clients via HttpClientHelpers

```csharp
// ✅ Good
var client = await HttpClientHelpers.GetAuthenticatedClientAsync<ICategoriesClient>();

// ❌ Bad — bypasses the standard tenant-header setup
var rawClient = App.CreateHttpClient("apiservice");
```

### 3. Discard Unused Assertion Results

```csharp
// ✅ Good — avoids IDE0058
_ = await Assert.That(response.IsSuccessStatusCode).IsTrue();

// ❌ Bad — triggers IDE0058 warning
await Assert.That(response.IsSuccessStatusCode).IsTrue();
```

### 4. Create All Data Inside Each Test

Tests must be self-contained. Never rely on data created by another test or the global seed beyond the default admin user:

```csharp
// ✅ Good — creates its own author
var author = await AuthorHelpers.CreateAuthorAsync(
    client, FakeDataGenerators.GenerateFakeAuthorRequest());

// ❌ Bad — depends on data that might not exist or was mutated by another test
var response = await client.GetAuthorAsync(KnownTestIds.SomeAuthorId);
```

### 5. Use Descriptive Test Names

```csharp
// ✅ Clear intent
UpdateBook_WithStaleETag_ShouldReturnPreconditionFailed

// ❌ Vague
TestBookUpdate
```

## Troubleshooting

### Authentication Failures (401)

- Verify `GlobalSetup` completed successfully (check test session output for startup errors).
- Ensure `GlobalHooks.AdminAccessToken` is not null before using `HttpClientHelpers`.
- For multi-tenant tests, confirm the tenant was created via `DatabaseHelpers.CreateTenantViaApiAsync` before calling `LoginAsAdminAsync`.

### SSE Event Timeout

- Confirm the mutation was sent to the correct tenant (`X-Tenant-ID` header).
- Verify the event type string — creation events often arrive as `*Updated` due to Marten upsert projection semantics.
- Use `ExecuteAndWaitForEventAsync` with multiple accepted types if unsure which fires.

### Database Unique Constraint Violations

- Always use `FakeDataGenerators` — never hardcode ISBNs, emails, or IDs.
- Use `Guid.CreateVersion7()` when an ID is set on the request (matching the project convention).

### Playwright Browser Missing

Build the project first, then install:

```bash
dotnet build tests/BookStore.AppHost.Tests/BookStore.AppHost.Tests.csproj
node tests/BookStore.AppHost.Tests/bin/Debug/net10.0/.playwright/package/index.js install chromium
```

### Aspire Startup Timeout

If the test session fails during setup with a health-check timeout:
- Ensure Docker is running (containers for PostgreSQL, Redis, Azurite are required).
- Increase the `healthCts` timeout in `GlobalSetup` if hardware is slow.

## Code Coverage

```bash
dotnet test tests/BookStore.AppHost.Tests/ --collect:"XPlat Code Coverage"
```

Coverage reports are written to the `TestResults/` directory.
