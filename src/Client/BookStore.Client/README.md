# BookStore.Client

Reusable .NET client library for the BookStore API.

## Features

- ✅ Type-safe API client using Refit
- ✅ Automatic JSON serialization
- ✅ Cancellation token support
- ✅ Easy dependency injection setup
- ✅ Works with any .NET project

## Installation

Add a project reference:

```xml
<ProjectReference Include="path/to/BookStore.Client/BookStore.Client.csproj" />
```

## Usage

### ASP.NET Core / Blazor

```csharp
using BookStore.Client;

var builder = WebApplication.CreateBuilder(args);

// Register the client
builder.Services
    .AddBookStoreClient(new Uri("https://api.bookstore.com"))
    .AddStandardResilienceHandler(); // Optional: Add Polly resilience

var app = builder.Build();
```

### Console Application

```csharp
using BookStore.Client;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddBookStoreClient(new Uri("https://api.bookstore.com"));

var provider = services.BuildServiceProvider();
var client = provider.GetRequiredService<IBookStoreApi>();

// Use the client
var books = await client.SearchBooks("clean code");
```

### Inject and Use

```csharp
public class BookService
{
    private readonly IBookStoreApi _api;

    public BookService(IBookStoreApi api)
    {
        _api = api;
    }

    public async Task<PagedListDto<BookDto>> SearchBooksAsync(
        string query,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        return await _api.SearchBooks(
            query, 
            pageNumber, 
            pageSize, 
            cancellationToken);
    }

    public async Task<BookDto> GetBookAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _api.GetBook(id, cancellationToken);
    }
}
```

## API Reference

See `IBookStoreApi` for all available endpoints:

- **Books**: Search, Get, Create, Update, Delete
- **Authors**: List, Get, Create, Update, Delete
- **Categories**: List, Get, Create, Update, Delete
- **Publishers**: List, Get, Create, Update, Delete

## Resilience (Optional)

The client returns `IHttpClientBuilder`, allowing you to add resilience policies:

```csharp
// Standard resilience (recommended)
builder.Services
    .AddBookStoreClient(apiUrl)
    .AddStandardResilienceHandler();

// Custom Polly policies
builder.Services
    .AddBookStoreClient(apiUrl)
    .AddPolicyHandler(retryPolicy)
    .AddPolicyHandler(circuitBreakerPolicy);
```

## Dependencies

- `Refit.HttpClientFactory` - Type-safe HTTP client
- `BookStore.Shared` - Shared DTOs and models
- `Microsoft.Extensions.DependencyInjection.Abstractions` - DI support
