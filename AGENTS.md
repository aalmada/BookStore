# BookStore — Agent Instructions

## Purpose
Short, authoritative guidance for agents working in this repository.

## Repo Summary
- **Stack**: Full-stack .NET 10 application: event-sourced ASP.NET Core API, Blazor frontend, Aspire orchestration.
- **Documentation**: See `docs/getting-started.md` and `README.md`.

## Architecture Highlights
- **Event Sourcing**: Domain events stored via Marten; aggregates rehydrated from event streams.
- **Real-time Updates**: Server-Sent Events (SSE) for mutation notifications to connected clients.
- **Aspire**: Orchestration for API, Web, PostgreSQL, and PgAdmin resources.
- **Localization**: Multi-language support for content and multi-currency for prices.
- **Multi-Tenancy**: Conjoined tenancy with data, API, and cache isolation.

## Build & Run
- **Restore**: `dotnet restore`
- **Run app (recommended)**: `aspire run` (starts API, Web, PostgreSQL, PgAdmin)
- **Run tests**: `dotnet test`
- **Format code**: `dotnet format`

## Key Coding Rules
- **Namespaces**: Use file-scoped namespaces: `namespace BookStore.Namespace;`.
- **DTOs/Commands/Events**: Prefer `record` types; enable nullable reference types.
- **Timestamps**: Use `DateTimeOffset` (UTC) and ISO 8601.
- **JSON**: camelCase properties; enums serialized as strings.
- **IDs**: Use `Guid.CreateVersion7()` (UUIDv7) where applicable.
- **Analyzer Rules**: Follow `docs/analyzer-rules.md` (events, commands, apply methods, handlers).

## Testing
- **Integration Tests**: Prefer `BookStore.AppHost.Tests`; name tests descriptively and assert specific properties.
- **CI**: Ensure `dotnet test` passes locally.

## Project Structure
- `src/ApiService/BookStore.ApiService`: Backend API (Domain, Aggregates, Commands, Events, Projections).
- `src/Web/BookStore.Web`: Blazor Frontend.
- `src/Client/BookStore.Client`: API Client / SDK.
- `src/Shared/BookStore.Shared`: Shared contracts, DTOs, and notification models.
- `src/BookStore.AppHost`: Aspire orchestration.

## Security Considerations

### Authentication & Authorization
- **JWT Tokens**: All protected endpoints require Bearer token in `Authorization` header.
- **Passkeys**: Passwordless authentication via WebAuthn/FIDO2 is fully supported.
- **Role-Based Access**: Admin endpoints require `Admin` role claim in JWT.
- **Never Hardcode Secrets**: Use `appsettings.json`, environment variables, or Azure Key Vault.

### Data Protection
- **Soft Deletion**: Use `Deleted` property; never hard-delete user data without consent.
- **Input Validation**: Always validate user input before processing commands.
- **SQL Injection**: Marten uses parameterized queries—no concern. For manual SQL, always parameterize.
- **XSS Protection**: Blazor automatically encodes output; be cautious with `@((MarkupString)...)`.

### Secret Management
```csharp
// ❌ Never do this
var apiKey = "hardcoded-secret-123";

// ✅ Use configuration
var apiKey = builder.Configuration["ApiKey"];

// ✅ Use Azure Key Vault (production)
builder.Configuration.AddAzureKeyVault(...);
```

### Correlation IDs
- **Always Include**: Use `X-Correlation-ID` header for tracing requests across services.
- **Generate Once**: Create at the entry point (Web frontend or API gateway) and propagate.
- **Never Log Sensitive Data**: Correlation IDs are logged; ensure they don't contain PII.

## Common Agent Mistakes & Troubleshooting

### Coding Mistakes

**❌ Using `Guid.NewGuid()` instead of `Guid.CreateVersion7()`**
```csharp
// ❌ Wrong - not UUIDv7
var id = Guid.NewGuid();

// ✅ Correct - UUIDv7 for time-ordered IDs
var id = Guid.CreateVersion7();
```

**❌ Using `DateTime` instead of `DateTimeOffset`**
```csharp
// ❌ Wrong - ambiguous timezone
var timestamp = DateTime.Now;

// ✅ Correct - explicit UTC timezone
var timestamp = DateTimeOffset.UtcNow;
```

**❌ Forgetting to use file-scoped namespaces**
```csharp
// ❌ Wrong - block-scoped namespace
namespace BookStore.ApiService {
    public class MyClass { }
}

// ✅ Correct - file-scoped namespace
namespace BookStore.ApiService;

public class MyClass { }
```

**❌ Event names in present/future tense**
```csharp
// ❌ Wrong - command-style naming
public record CreateBook(...);

// ✅ Correct - past tense for events
public record BookCreated(...);
```

### Architecture Mistakes

**❌ Adding business logic to Endpoints**
```csharp
// ❌ Wrong - logic in endpoint
app.MapPost("/api/books", (CreateBookRequest req) => {
    if (req.Price < 0) throw new Exception("Invalid price");
    // ...
});

// ✅ Correct - logic in handler or aggregate
public static async Task<IResult> Handle(CreateBookCommand cmd, ...) {
    // Business validation here
}
```

**❌ Forgetting SSE notifications**
```csharp
// ❌ Wrong - no real-time updates
public record BookCreated(Guid Id);

// ✅ Correct - add notification + listener
// 1. Create BookCreatedNotification in Shared
// 2. Update MartenCommitListener
// 3. Map in QueryInvalidationService
```

**❌ Not using HybridCache for queries**
```csharp
// ❌ Wrong - no caching
var books = await session.Query<BookProjection>().ToListAsync();

// ✅ Correct - use HybridCache
var books = await cache.GetOrCreateAsync("books", async entry => {
    return await session.Query<BookProjection>().ToListAsync();
});
```

**❌ Configuring Marten in Program.cs**
```csharp
// ❌ Wrong - redundant and missing options
builder.Services.AddMarten(options => { ... });

// ✅ Correct - use extension method
builder.Services.AddMartenEventStore(builder.Configuration);
// Configure projections in MartenConfigurationExtensions.cs
```

### Testing Mistakes

**❌ Using xUnit/NUnit instead of TUnit**
```csharp
// ❌ Wrong framework
[Fact]
public void TestSomething() { }

// ✅ Correct - use TUnit
[Test]
public async Task TestSomething() { 
    await Assert.That(result).IsNotNull();
}
```

**❌ Not using TestHelpers for integration tests**
```csharp
// ❌ Wrong - manual HTTP calls
var response = await httpClient.PostAsync("/api/books", content);

// ✅ Correct - use TestHelpers with SSE verification
var book = await TestHelpers.CreateBookAsync(client, request);
// Automatically waits for BookUpdated event
```

### Debugging Tips

**Build Failures**
1. Check analyzer errors (BS1xxx-BS4xxx) first
2. Run `dotnet clean && dotnet build`
3. Ensure all project references are correct

**Test Failures**
1. Check if Aspire is already running (port conflicts)
2. Verify TUnit assertions (not xUnit/NUnit syntax)
3. Check SSE event subscriptions in integration tests

**SSE Not Working**
1. Verify `MartenCommitListener` has handler for your projection
2. Check `QueryInvalidationService` maps event to query keys
3. Ensure frontend calls `EventsService.StartListening()`

**Cache Not Invalidating**
1. Check cache tags match in `GetOrCreateAsync` and `RemoveByTagAsync`
2. Verify SSE notifications trigger invalidation
3. Check Redis connection (if using distributed cache)

## Which AGENTS.md File to Read?

- **Starting a new feature?** → Read relevant project AGENTS.md (ApiService, Web, or Client)
- **Testing?** → Read test project AGENTS.md (AppHost.Tests, UnitTests, etc.)
- **Deployment?** → Read AppHost AGENTS.md + docs/aspire-deployment-guide.md
- **Cross-cutting concerns?** → Read ServiceDefaults AGENTS.md
- **Not sure?** → Start with this root AGENTS.md, then follow references

## Agent Skills
Use Claude skills for common tasks:

### Scaffolding
- `/scaffold-write` - Add new command/mutation endpoint
- `/scaffold-read` - Add new query endpoint
- `/scaffold-frontend-feature` - Add Blazor feature with reactive state
- `/scaffold-aggregate` - Create event-sourced aggregate with Apply methods
- `/scaffold-projection` - Create Marten read model projection
- `/scaffold-test` - Create integration test with SSE verification
- `/scaffold-skill` - Create new agent skill

### Verification & Testing
- `/verify-feature` - Run build, format, and tests
- `/run-integration-tests` - Run integration test suite
- `/run-unit-tests` - Run unit test suites

### Debugging
- `/debug-sse` - Troubleshoot Server-Sent Events issues
- `/debug-cache` - Troubleshoot HybridCache/Redis issues

### Deployment & Operations
- `/deploy-to-azure` - Deploy to Azure Container Apps using azd
- `/deploy-kubernetes` - Deploy to Kubernetes cluster with manifests
- `/rollback-deployment` - Rollback failed deployment to previous version

### Utilities
- `/doctor` - Check development environment
- `/rebuild-clean` - Clean build from scratch
