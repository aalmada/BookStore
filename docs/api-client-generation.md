# API Client Generation

## Overview

The BookStore API client is provided as a reusable library (`BookStore.Client`) that can be used by any .NET project. The client uses **Refit** for type-safe HTTP calls and is **manually maintained** to ensure clean, predictable interfaces.

## Manual Refit Approach

Instead of automated code generation, we manually create Refit interfaces. This approach provides:

✅ **Full Control** - Exact interface design  
✅ **No Build Dependencies** - No code generation tools required  
✅ **Clean Interfaces** - Hand-crafted, readable code  
✅ **Shared DTOs** - Models in `BookStore.Shared` for API/Client reuse  
✅ **Type Safety** - Compile-time checking with Refit  

### Architecture

```
BookStore.Shared/Models/
├── BookDto.cs              # Shared DTOs
├── AuthorDto.cs
├── CategoryDto.cs
├── PublisherDto.cs
├── IdentityModels.cs       # Authentication DTOs
└── ...

BookStore.Client/
├── IGetBooksEndpoint.cs    # Query endpoints
├── IGetBookEndpoint.cs
├── ICreateBookEndpoint.cs  # Command endpoints
├── IIdentityEndpoints.cs   # Authentication endpoints
└── BookStoreClientExtensions.cs  # DI registration
```

## Creating New Endpoints

### 1. Define Shared DTOs (if needed)

If the endpoint uses new models, add them to `BookStore.Shared/Models/`:

```csharp
namespace BookStore.Shared.Models;

public record MyNewDto(
    Guid Id,
    string Name,
    // ... other properties
);
```

### 2. Create Refit Interface

Add a new interface in `BookStore.Client/`:

```csharp
using BookStore.Shared.Models;
using Refit;

namespace BookStore.Client;

public interface IMyNewEndpoint
{
    [Get("/api/my-resource/{id}")]
    Task<MyNewDto> Execute(
        Guid id,
        [Header("api-version")] string api_version,
        [Header("Accept-Language")] string accept_Language,
        [Header("X-Correlation-ID")] string x_Correlation_ID,
        [Header("X-Causation-ID")] string x_Causation_ID,
        CancellationToken cancellationToken = default);
}
```

### 3. Register in DI

Add to `BookStoreClientExtensions.cs`:

```csharp
_ = services.AddRefitClient<IMyNewEndpoint>()
    .ConfigureHttpClient(c => c.BaseAddress = baseAddress);
```

## Common Patterns

### Query Endpoints (GET)

```csharp
public interface IGetBooksEndpoint
{
    [Get("/api/books")]
    Task<PagedListDto<BookDto>> Execute(
        [Query] int pageNumber,
        [Query] int pageSize,
        [Header("api-version")] string api_version,
        [Header("Accept-Language")] string accept_Language,
        [Header("X-Correlation-ID")] string x_Correlation_ID,
        [Header("X-Causation-ID")] string x_Causation_ID,
        CancellationToken cancellationToken = default);
}
```

### Command Endpoints (POST/PUT)

```csharp
public interface ICreateBookEndpoint
{
    [Post("/api/admin/books")]
    Task<IApiResponse> Execute(
        [Body] CreateBookRequest request,
        [Header("api-version")] string api_version,
        [Header("X-Correlation-ID")] string x_Correlation_ID,
        [Header("X-Causation-ID")] string x_Causation_ID,
        CancellationToken cancellationToken = default);
}
```

### Authentication Endpoints

```csharp
public interface IIdentityLoginEndpoint
{
    [Post("/identity/login")]
    Task<LoginResponse> Execute(
        [Body] LoginRequest request,
        [AliasAs("useCookies")] bool? useCookies = null,
        [AliasAs("useSessionCookies")] bool? useSessionCookies = null,
        CancellationToken cancellationToken = default);
}
```

## Using the Client

### Registration

```csharp
// In Program.cs
var apiServiceUrl = builder.Configuration["services:apiservice:https:0"]
    ?? "http://localhost:5000";

builder.Services.AddBookStoreClient(new Uri(apiServiceUrl));
```

### Injection and Usage

```csharp
public class MyService(IGetBooksEndpoint booksEndpoint)
{
    public async Task<PagedListDto<BookDto>> GetBooksAsync()
    {
        return await booksEndpoint.Execute(
            pageNumber: 1,
            pageSize: 20,
            api_version: "1.0",
            accept_Language: "en",
            x_Correlation_ID: Guid.NewGuid().ToString(),
            x_Causation_ID: string.Empty);
    }
}
```

## Benefits of Manual Approach

### vs. Automated Generation

| Aspect | Manual Refit | Automated (Refitter/NSwag) |
|--------|--------------|----------------------------|
| **Control** | ✅ Full control | ⚠️ Generated code |
| **Dependencies** | ✅ None | ❌ Build-time tools |
| **Readability** | ✅ Hand-crafted | ⚠️ Generated |
| **Maintenance** | ⚠️ Manual updates | ✅ Auto-sync |
| **Customization** | ✅ Easy | ❌ Limited |
| **Build Speed** | ✅ Fast | ⚠️ Slower |
| **Type Conflicts** | ✅ Avoided | ❌ Common issue |

### When to Use Manual Approach

✅ **Small to medium APIs** - Manageable number of endpoints  
✅ **Stable APIs** - Infrequent changes  
✅ **Custom requirements** - Need specific interface design  
✅ **Shared models** - DTOs used by both API and client  
✅ **Clean architecture** - Want predictable, readable code  

## Authentication Integration

The BookStore uses **hybrid authentication**:

### Cookie Authentication (Blazor Frontend)

For the Blazor frontend, authentication cookies are sent automatically by the browser. No manual token injection is needed:

```csharp
// Cookies are sent automatically with every request
var books = await booksEndpoint.Execute(
    pageNumber: 1,
    pageSize: 20,
    api_version: "1.0",
    accept_Language: "en",
    x_Correlation_ID: Guid.NewGuid().ToString(),
    x_Causation_ID: string.Empty);
```

### JWT Bearer Tokens (External Apps)

For external applications (mobile apps, third-party integrations), use JWT bearer tokens:

```csharp
// Add JWT token to Authorization header
builder.Services.ConfigureHttpClientDefaults(http =>
{
    http.ConfigureHttpClient(client =>
    {
        client.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", accessToken);
    });
});
```

See [Authentication Guide](authentication-guide.md) for details on cookie vs JWT authentication.

## Best Practices

### 1. Use Shared DTOs

Place DTOs in `BookStore.Shared/Models/` to avoid duplication:

```csharp
// ✅ Good - Shared DTO
namespace BookStore.Shared.Models;
public record BookDto(...);

// ❌ Bad - Duplicate in Client
namespace BookStore.Client;
public record BookDto(...);
```

### 2. Consistent Naming

Follow the pattern `I{Action}{Resource}Endpoint`:

```csharp
IGetBooksEndpoint
IGetBookEndpoint
ICreateBookEndpoint
IUpdateBookEndpoint
IDeleteBookEndpoint
```

### 3. Include Standard Headers

Always include API version, correlation ID, and causation ID:

```csharp
[Header("api-version")] string api_version,
[Header("X-Correlation-ID")] string x_Correlation_ID,
[Header("X-Causation-ID")] string x_Causation_ID
```

### 4. Use CancellationToken

Support cancellation for all async operations:

```csharp
Task<T> Execute(..., CancellationToken cancellationToken = default);
```

### 5. Return Appropriate Types

- **Queries**: Return `Task<TDto>` or `Task<PagedListDto<TDto>>`
- **Commands**: Return `Task<IApiResponse>` or `Task`
- **Authentication**: Return specific response types

## Testing

### Unit Testing

Mock Refit interfaces for testing:

```csharp
var mockEndpoint = Substitute.For<IGetBooksEndpoint>();
mockEndpoint.Execute(Arg.Any<int>(), ...)
    .Returns(new PagedListDto<BookDto>(...));

var service = new MyService(mockEndpoint);
var result = await service.GetBooksAsync();
```

### Integration Testing

Use `WebApplicationFactory` to test against real API:

```csharp
var client = factory.Services.GetRequiredService<IGetBooksEndpoint>();
var books = await client.Execute(1, 20, ...);
Assert.NotEmpty(books.Items);
```

## Troubleshooting

### "Could not resolve type"

Ensure the DTO is in `BookStore.Shared` and referenced:

```csharp
using BookStore.Shared.Models;
```

### "No service for type"

Register the endpoint in `BookStoreClientExtensions.cs`:

```csharp
_ = services.AddRefitClient<IMyEndpoint>()
    .ConfigureHttpClient(c => c.BaseAddress = baseAddress);
```

### "401 Unauthorized"

Ensure `AuthorizingHttpMessageHandler` is configured and user is logged in.

## Related Documentation

- [Refit Documentation](https://github.com/reactiveui/refit)
- [Authentication Guide](authentication-guide.md)
- [API Conventions](api-conventions-guide.md)
