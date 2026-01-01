# API Client Generation Guide

This guide explains how to use and maintain the BookStore API client library.

> [!NOTE]
> **Build-Time Generation**: Fully automatic compile-time client generation is not currently possible because .NET 9's `Microsoft.Extensions.ApiDescription.Server` requires running the application (including database connections) to generate the OpenAPI spec. We use **Refitter** to generate Refit interfaces + DTOs from the runtime-generated `openapi.json` file.

## Overview

The BookStore API client is provided as a reusable library (`BookStore.Client`) that can be used by any .NET project. The client is automatically generated from the OpenAPI specification using **Refitter**.

### What is Refitter?

[Refitter](https://github.com/christianhelle/refitter) is a tool that generates Refit interfaces and DTOs from OpenAPI specifications. It uses NSwag internally for DTO generation but creates clean Refit interfaces instead of full HTTP client implementations.

**Benefits**:
- ✅ Automatic generation from OpenAPI spec
- ✅ Clean Refit interfaces (not verbose HTTP client code)
- ✅ Type-safe DTOs
- ✅ Uses Refit's proven HTTP client
- ✅ Much smaller output than NSwag alone (~1,300 lines vs 4,489 lines)

## Installation

### For .NET Projects

```bash
dotnet add package BookStore.Client
```

Or add to your `.csproj`:

```xml
<ItemGroup>
  <ProjectReference Include="../path/to/BookStore.Client/BookStore.Client.csproj" />
</ItemGroup>
```

### Register the Client

```csharp
using BookStore.Client;

// In Program.cs or Startup.cs
builder.Services.AddBookStoreClient(new Uri("https://api.bookstore.com"));
```

### With Resilience Policies (Polly)

```csharp
// Configure Polly policies
var retryPolicy = HttpPolicyExtensions
    .HandleTransientHttpError()
    .WaitAndRetryAsync(3, retryAttempt => 
        TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

var circuitBreakerPolicy = HttpPolicyExtensions
    .HandleTransientHttpError()
    .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));

// Register client
builder.Services.AddBookStoreClient(new Uri(apiServiceUrl));

// Apply policies to all HTTP clients
builder.Services.ConfigureHttpClientDefaults(http =>
{
    http.AddPolicyHandler(retryPolicy);
    http.AddPolicyHandler(circuitBreakerPolicy);
});
```

## Usage

### Inject Specific Endpoints

Refitter generates one interface per endpoint, giving you fine-grained control:

```csharp
public class BookService
{
    private readonly IGetBooksEndpoint _getBooksEndpoint;
    private readonly IGetBookEndpoint _getBookEndpoint;
    private readonly ICreateBookEndpoint _createBookEndpoint;

    public BookService(
        IGetBooksEndpoint getBooksEndpoint,
        IGetBookEndpoint getBookEndpoint,
        ICreateBookEndpoint createBookEndpoint)
    {
        _getBooksEndpoint = getBooksEndpoint;
        _getBookEndpoint = getBookEndpoint;
        _createBookEndpoint = createBookEndpoint;
    }

    public async Task<PagedListDtoOfBookDto> GetBooksAsync(int page = 1, int pageSize = 20)
    {
        return await _getBooksEndpoint.Execute(
            page: page,
            pageSize: pageSize,
            search: null,
            api_version: "1.0",
            accept_Language: "en",
            x_Correlation_ID: Guid.NewGuid().ToString(),
            x_Causation_ID: null);
    }
}
```

### Available Endpoint Interfaces

**Public Endpoints**:
- `IGetBooksEndpoint` - Get all books with pagination
- `IGetBookEndpoint` - Get book by ID
- `IGetAuthorsEndpoint` - Get all authors
- `IGetAuthorEndpoint` - Get author by ID
- `IGetCategoriesEndpoint` - Get all categories
- `IGetCategoryEndpoint` - Get category by ID
- `IGetPublishersEndpoint` - Get all publishers
- `IGetPublisherEndpoint` - Get publisher by ID

**Admin Endpoints**:
- `ICreateBookEndpoint`, `IUpdateBookEndpoint`, `ISoftDeleteBookEndpoint`, `IRestoreBookEndpoint`
- `ICreateAuthorEndpoint`, `IUpdateAuthorEndpoint`, `ISoftDeleteAuthorEndpoint`, `IRestoreAuthorEndpoint`
- `ICreateCategoryEndpoint`, `IUpdateCategoryEndpoint`, `ISoftDeleteCategoryEndpoint`, `IRestoreCategoryEndpoint`
- `ICreatePublisherEndpoint`, `IUpdatePublisherEndpoint`, `ISoftDeletePublisherEndpoint`, `IRestorePublisherEndpoint`
- `IUploadBookCoverEndpoint`

**System Endpoints**:
- `IRebuildProjectionsEndpoint` - Rebuild read model projections
- `IGetProjectionStatusEndpoint` - Get projection daemon status

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
   
   This downloads the latest spec from the running API to `openapi.json`.

3. **Regenerate Client**:
   ```bash
   ./_tools/generate-client.sh
   ```
   
   This runs Refitter to regenerate all interfaces and DTOs.

4. **Build and Test**:
   ```bash
   dotnet build
   dotnet test
   ```

5. **Commit Changes**:
   ```bash
   git add openapi.json src/Client/BookStore.Client/
   git commit -m "Update API client from OpenAPI spec"
   ```

### Refitter Configuration

The `.refitter` file in the repository root configures Refitter:

```json
{
  "openApiPath": "openapi.json",
  "namespace": "BookStore.Client",
  "naming": {
    "useOpenApiTitle": false,
    "interfaceName": "I{controller}Endpoint"
  },
  "generateContracts": true,
  "multipleInterfaces": "ByEndpoint",
  "useIsoDateFormat": true,
  "outputFolder": "src/Client/BookStore.Client",
  "contractsOutputFolder": "src/Client/BookStore.Client",
  "contractsOutputFilename": "Contracts.cs",
  "additionalNamespaces": [
    "BookStore.Shared.Models"
  ],
  "codeGeneratorSettings": {
    "excludedTypeNames": [
      "PartialDate",
      "BookDto",
      "AuthorDto",
      "CategoryDto",
      "PublisherDto",
      "PagedListDto"
    ]
  }
}
```

**Key Settings**:
- `multipleInterfaces: "ByEndpoint"` - Generates one interface per endpoint
- `generateContracts: true` - Generates DTOs in `Contracts.cs`
- `useIsoDateFormat: true` - Ensures compatible ISO 8601 date handling
- `additionalNamespaces` - Adds `using BookStore.Shared.Models` to generated files
- `excludedTypeNames` - Prevents regeneration of shared DTOs defined in `BookStore.Shared`
- `outputFolder` - Where to generate interface files

### Install Refitter (Optional)

If you want to run client generation locally:

```bash
dotnet tool install --global Refitter
```

### Generated Files

After running `generate-client.sh`, you'll have:

- **28 Interface Files**: `I*Endpoint.cs` (e.g., `IGetBooksEndpoint.cs`)
- **1 Contracts File**: `Contracts.cs` with all DTOs (~624 lines)

All files are auto-generated and should not be manually edited.

### Why Refitter Instead of NSwag?

**NSwag Alone**:
- ❌ Generates full HTTP client implementation (~3,200 lines)
- ❌ Verbose code with custom HTTP handling
- ❌ Harder to customize

**Refitter (NSwag + Refit)**:
- ✅ Generates clean Refit interfaces (~700 lines)
- ✅ Uses proven Refit HTTP client
- ✅ Smaller, more maintainable code
- ✅ Easy to apply Polly policies

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
- ✅ Use **Refitter** to generate client from saved spec
- ✅ Simple, reliable, works in all environments

## Update Scripts

### `update-openapi.sh`

The `update-openapi.sh` script:
- Auto-detects Aspire's dynamic ports
- Downloads OpenAPI spec from running API
- Saves to `openapi.json` in repository root

### `generate-client.sh`

The `generate-client.sh` script:
- Runs Refitter with `.refitter` configuration
- Generates 28 endpoint interfaces
- Generates `Contracts.cs` with all DTOs
- Uses `--skip-validation` flag (OpenAPI 3.1.1 support)

## Architecture

```
┌─────────────────────────────────────────┐
│         Your .NET Application           │
│                                         │
│  Injects: IGetBooksEndpoint, etc.      │
└────────────────┬────────────────────────┘
                 │
                 │ Uses
                 ▼
┌─────────────────────────────────────────┐
│       BookStore.Client Library          │
│                                         │
│  ┌───────────────────────────────────┐ │
│  │  28 Refit Interfaces              │ │
│  │  (I*Endpoint.cs)                  │ │
│  └───────────────────────────────────┘ │
│  ┌───────────────────────────────────┐ │
│  │  Contracts.cs (DTOs)              │ │
│  │  - BookDto, AuthorDto, etc.       │ │
│  └───────────────────────────────────┘ │
│  ┌───────────────────────────────────┐ │
│  │  BookStoreClientExtensions.cs     │ │
│  │  - DI registration helpers        │ │
│  └───────────────────────────────────┘ │
└────────────────┬────────────────────────┘
                 │
                 │ Uses
                 ▼
┌─────────────────────────────────────────┐
│         Refit (HTTP Client)             │
└────────────────┬────────────────────────┘
                 │
                 │ HTTP/REST
                 ▼
┌─────────────────────────────────────────┐
│       BookStore API Service             │
└─────────────────────────────────────────┘
```

## Examples

### Console Application

```csharp
using BookStore.Client;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddBookStoreClient(new Uri("https://api.bookstore.com"));

var provider = services.BuildServiceProvider();
var getBooksEndpoint = provider.GetRequiredService<IGetBooksEndpoint>();

var books = await getBooksEndpoint.Execute(
    page: 1,
    pageSize: 20,
    search: "Clean Code",
    api_version: "1.0",
    accept_Language: "en",
    x_Correlation_ID: Guid.NewGuid().ToString(),
    x_Causation_ID: null);

foreach (var book in books.Items)
{
    Console.WriteLine($"{book.Title} by {book.AuthorNames}");
}
```

### Blazor Component

```razor
@inject IGetBooksEndpoint GetBooksEndpoint

<MudDataGrid Items="@books">
    <Columns>
        <PropertyColumn Property="x => x.Title" />
        <PropertyColumn Property="x => x.AuthorNames" />
    </Columns>
</MudDataGrid>

@code {
    private List<BookDto> books = new();

    protected override async Task OnInitializedAsync()
    {
        var result = await GetBooksEndpoint.Execute(
            page: 1,
            pageSize: 20,
            search: null,
            api_version: "1.0",
            accept_Language: "en",
            x_Correlation_ID: Guid.NewGuid().ToString(),
            x_Causation_ID: null);
        
        books = result.Items.ToList();
    }
}
```

## Troubleshooting

### "Refitter not found"

Install Refitter globally:
```bash
dotnet tool install --global Refitter
```

### "OpenAPI 3.1.1 not supported"

The scripts use `--skip-validation` flag to work around this. Refitter's validation doesn't support OpenAPI 3.1.1 yet, but generation works fine.

### Build Errors After Regeneration

1. Clean and rebuild:
   ```bash
   dotnet clean
   dotnet build
   ```

2. Check for breaking changes in the OpenAPI spec
3. Review generated files for issues

## See Also

- [Refitter Documentation](https://github.com/christianhelle/refitter)
- [Refit Documentation](https://github.com/reactiveui/refit)
- [Polly Documentation](https://github.com/App-vNext/Polly)


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
