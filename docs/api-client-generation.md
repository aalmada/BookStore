# API Client Generation Guide

This guide explains how to use the BookStore API client library.

> [!NOTE]
> **Build-Time Generation**: Fully automatic compile-time client generation is not currently possible because .NET 9's `Microsoft.Extensions.ApiDescription.Server` requires running the application (including database connections) to generate the OpenAPI spec. We use a runtime generation approach with manual updates or optional NSwag auto-generation from the saved `openapi.json` file.

## Overview

The BookStore API client is provided as a reusable library (`BookStore.Client`) that can be used by any .NET project.

**Technology Stack**:
- **Refit**: Type-safe REST library
- **BookStore.Client**: Standalone client library
- **OpenAPI Spec**: Generated at runtime for documentation

## Using the Client Library

### Installation

Add a project reference to `BookStore.Client`:

```xml
<ProjectReference Include="path/to/BookStore.Client/BookStore.Client.csproj" />
```

### Registration

#### ASP.NET Core / Blazor

```csharp
using BookStore.Client;

var builder = WebApplication.CreateBuilder(args);

// Register the client
builder.Services
    .AddBookStoreClient(new Uri("https://api.bookstore.com"))
    .AddStandardResilienceHandler(); // Optional: Add Polly resilience

var app = builder.Build();
```

#### Console Application

```csharp
using BookStore.Client;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddBookStoreClient(new Uri("https://api.bookstore.com"));

var provider = services.BuildServiceProvider();
var client = provider.GetRequiredService<IBookStoreApi>();

// Use the client
var books = await client.GetBooksAsync("clean code");
```

### Usage

Inject `IBookStoreApi` into your services:

```csharp
public class BookService
{
    private readonly IBookStoreApi _api;

    public BookService(IBookStoreApi api)
    {
        _api = api;
    }

    public async Task<PagedListDto<BookDto>> SearchBooksAsync(
        string? query = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        return await _api.GetBooksAsync(query, page, pageSize, cancellationToken);
    }
}
```

## Available Endpoints

See `IBookStoreApi` for all available methods:

- **Books**: `GetBooksAsync`, `GetBookAsync`
- **Authors**: `GetAuthorsAsync`, `GetAuthorAsync`
- **Categories**: `GetCategoriesAsync`, `GetCategoryAsync`
- **Publishers**: `GetPublishersAsync`, `GetPublisherAsync`

## Resilience (Optional)

Add Polly resilience policies:

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

## Maintaining the Client

### When API Changes

**Workflow**:

1. **Start the API**:
   ```bash
   aspire run
   ```

2. **Update OpenAPI Spec**:
   ```bash
   ./_tools/update-openapi.sh
   ```

3. **Update Client Interface** (choose one):

   **Option A: NSwag Auto-Generation** (optional):
   ```bash
   ./_tools/generate-client-nswag.sh
   ```
   
   **Option B: Manual Update**:
   - Edit `src/Client/BookStore.Client/IBookStoreApi.cs`
   - Add/update methods to match API changes

4. **Build and Test**:
   ```bash
   dotnet build
   dotnet test
   ```

5. **Commit Changes**:
   ```bash
   git add openapi.json src/Client/BookStore.Client/IBookStoreApi.cs
   git commit -m "Update API client: [description]"
   ```

### NSwag Auto-Generation (Optional)

NSwag can automatically generate the Refit interface from the OpenAPI spec.

**Install NSwag CLI**:
```bash
dotnet tool install --global NSwag.ConsoleCore
```

**Pros**:
- ✅ Automatic - generates entire interface
- ✅ Consistent - always matches OpenAPI spec
- ✅ Fast - one command

**Cons**:
- ❌ Requires NSwag CLI installed
- ❌ Generates full client implementation (not just Refit interface)
- ❌ Generates DTOs (conflicts with `BookStore.Shared`)
- ❌ Uses Newtonsoft.Json (we use System.Text.Json)
- ❌ Needs manual cleanup and conversion to Refit

> [!NOTE]
> NSwag's default output generates a complete client implementation with DTOs, which conflicts with our architecture where DTOs are in `BookStore.Shared`. Converting NSwag output to a clean Refit interface requires significant manual work, making the manual approach more practical for this project.

**Current Approach**: We use **manual updates** for clean, minimal code. NSwag is available as a reference tool to see what endpoints exist, but the generated code requires extensive modification.

### Why Not Build-Time Generation?

Build-time OpenAPI generation is not currently feasible because:

1. **`Microsoft.Extensions.ApiDescription.Server` runs the application** during build
2. **Requires database connection** - The app needs PostgreSQL to start
3. **Infrastructure dependencies** - Marten, Wolverine, and other services must initialize
4. **CI/CD complexity** - Would need database available during builds

**Microsoft's Documentation** explicitly states:
> "Build-time OpenAPI document generation functions by launching the app's entrypoint with a mock server implementation."

This means the entire application stack runs, including all service registrations and infrastructure dependencies.

**Our Solution**:
- ✅ Generate OpenAPI at **runtime** from running API
- ✅ Save `openapi.json` to git (tracks API changes)
- ✅ Update client **manually** or with **NSwag** from saved spec
- ✅ Simple, reliable, works in all environments

### Update Scripts

The `update-openapi.sh` script:
- Auto-detects Aspire's dynamic ports
- Downloads the OpenAPI spec
- Validates the file

## OpenAPI Specification

**Location**: `openapi.json` (repository root)

**Purpose**:
- API contract documentation
- Scalar UI documentation (`/scalar/v1`)
- Contract validation
- Manual client updates

**Format**: OpenAPI 3.1.1 (generated by .NET 9)

## References

- [Refit Documentation](https://github.com/reactiveui/refit)
- [OpenAPI 3.1 Specification](https://spec.openapis.org/oas/v3.1.0)
- [Client Library README](../src/Client/BookStore.Client/README.md)
