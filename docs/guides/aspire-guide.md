# Aspire Orchestration Guide

This guide explains how the BookStore solution uses **Aspire** to orchestrate distributed services, manage local development resources, and simplify cloud-native application composition.

## Overview

The BookStore solution is built as a distributed system composed of:
1.  **API Service**: Event-sourced backend
2.  **Web Frontend**: Blazor interactive UI
3.  **Infrastructure**: PostgreSQL, Redis, and Azure Blob Storage

Aspire serves as the glue that binds these components together, handling:
- **Service Discovery**: Automatically injecting connection strings and service URLs.
- **Resource Management**: Spinning up containers for databases and emulators during development.
- **Observability**: Providing a unified dashboard for logs, traces, and metrics.

## AppHost: The Orchestrator

The `BookStore.AppHost` project is the entry point for the distributed application. It defines the architectural blueprint of the system in C# inside `AppHost.cs`.

### Resource Names Constants

All resource names are defined as constants in `ResourceNames` (in `BookStore.ServiceDefaults`) to avoid magic strings and ensure consistency between the AppHost and consuming services:

```csharp
public static class ResourceNames
{
    public const string Postgres = "postgres";
    public const string BookStoreDb = "bookstore";
    public const string Storage = "storage";
    public const string Blobs = "blobs";
    public const string Cache = "cache";
    public const string ApiService = "apiservice";
    public const string WebFrontend = "webfrontend";

    public const string HealthCheckEndpoint = "/health";
    public const string ApiReferenceText = "API Reference";
    public const string ApiReferenceUrl = "/api-reference";
}
```

Always use these constants when referencing resources — never hardcode strings.

### Defined Resources

The AppHost defines the following resources in `AppHost.cs`:

#### 1. Databases & Storage
-   **PostgreSQL**: A containerized PostgreSQL instance with PgAdmin.
    ```csharp
    var postgres = builder.AddPostgres(ResourceNames.Postgres)
        .WithPgAdmin();
    var bookStoreDb = postgres.AddDatabase(ResourceNames.BookStoreDb);
    ```
-   **Redis**: Distributed cache for hybrid caching.
    ```csharp
    var cache = builder.AddRedis(ResourceNames.Cache);
    ```
-   **Azure Storage**: Uses the **Azurite** emulator for local development, providing Blob storage compatible with Azure.
    ```csharp
    var storage = builder.AddAzureStorage(ResourceNames.Storage)
        .RunAsEmulator();
    var blobs = storage.AddBlobs(ResourceNames.Blobs);
    ```

#### 2. Services
-   **API Service** (`Projects.BookStore_ApiService`): The backend API. Waits for both PostgreSQL and Redis before starting, exposes external HTTP endpoints, and registers a health check.
    ```csharp
    var apiService = builder.AddProject<Projects.BookStore_ApiService>(ResourceNames.ApiService)
        .WithReference(bookStoreDb)
        .WithReference(blobs)
        .WithReference(cache)
        .WaitFor(cache)
        .WaitFor(postgres)
        .WithHttpHealthCheck(ResourceNames.HealthCheckEndpoint)
        .WithExternalHttpEndpoints()
        .WithUrlForEndpoint("http", url =>
        {
            url.DisplayText = ResourceNames.ApiReferenceText;
            url.Url += ResourceNames.ApiReferenceUrl;
        })
        .WithUrlForEndpoint("https", url =>
        {
            url.DisplayText = ResourceNames.ApiReferenceText;
            url.Url += ResourceNames.ApiReferenceUrl;
        });
    ```
-   **Web Frontend** (`Projects.BookStore_Web`): The Blazor app. Waits for the API service before starting.
    ```csharp
    builder.AddProject<Projects.BookStore_Web>(ResourceNames.WebFrontend)
        .WithExternalHttpEndpoints()
        .WithHttpHealthCheck(ResourceNames.HealthCheckEndpoint)
        .WithReference(apiService)
        .WaitFor(apiService);
    ```

### Environment Variable Forwarding

The AppHost conditionally forwards configuration to the API service. These are typically set in `appsettings.Development.json` or via environment variables when running tests:

```csharp
// Disable rate limiting (used in integration tests)
var disableRateLimit = builder.Configuration["RateLimit:Disabled"];
if (!string.IsNullOrEmpty(disableRateLimit))
    apiService.WithEnvironment("RateLimit__Disabled", disableRateLimit);

// Enable database seeding
var seedingEnabled = builder.Configuration["Seeding:Enabled"];
if (!string.IsNullOrEmpty(seedingEnabled))
    apiService.WithEnvironment("Seeding__Enabled", seedingEnabled);

// Email delivery method (e.g. "Smtp", "None")
var emailDeliveryMethod = builder.Configuration["Email:DeliveryMethod"];
if (!string.IsNullOrEmpty(emailDeliveryMethod))
    apiService.WithEnvironment("Email__DeliveryMethod", emailDeliveryMethod);
```

### Service Discovery

Aspire automates connection management using the resource names defined in the AppHost.
-   **Databases**: The API service receives the connection string for `"bookstore"` automatically via `WithReference(bookStoreDb)`.
-   **HTTP Services**: The frontend receives the base URL for `"apiservice"` via service discovery, allowing `HttpClient` to use `http://apiservice` as the base address.

## Service Defaults: Shared Concerns

The `BookStore.ServiceDefaults` project encapsulates cross-cutting concerns that every service needs. Both the API and Web projects reference this library and call `builder.AddServiceDefaults()` in their startup.

### Features

-   **OpenTelemetry**: Configures logging, tracing, and metrics export to the Aspire Dashboard.
    -   ASP.NET Core and HTTP client instrumentation
    -   Wolverine message-bus metrics and traces (`AddMeter("Wolverine")`, `AddSource("Wolverine")`)
    -   Custom `BookStore.ApiService` meter
    -   Health check requests are excluded from traces
    -   OTLP export when `OTEL_EXPORTER_OTLP_ENDPOINT` is set
-   **Console Logging Formatters**:
    -   Development: human-readable `SimpleConsole` formatter with timestamps and scopes
    -   Production: structured `JsonConsole` formatter with UTC ISO-8601 timestamps
-   **Health Checks**: Adds `/health` (readiness) and `/alive` (liveness) endpoints. Both endpoints are decorated with `AllowAnonymousTenantAttribute` so they bypass tenant-aware middleware.
-   **HTTP Client Defaults**: All `HttpClient` instances automatically get:
    -   `AddStandardResilienceHandler()` — retry, circuit breaker, and timeout policies
    -   `AddServiceDiscovery()` — resolves Aspire resource names to endpoints

## Local Development Experience

The root `aspire.config.json` points to the AppHost project, allowing `aspire run` to work from any directory in the repository:

```json
{
  "appHost": {
    "path": "src/BookStore.AppHost/BookStore.AppHost.csproj"
  }
}
```

To start the entire solution:

```bash
aspire run
# or
dotnet run --project src/BookStore.AppHost/BookStore.AppHost.csproj
```

> **Prerequisite**: Docker Desktop must be running — PostgreSQL, Redis, and Azurite are all containerised.

### The Aspire Dashboard

When the application starts, the **Aspire Dashboard** launches automatically. It provides a centralized view of:
-   **Resources**: Status and endpoints of all running services and containers.
-   **Console Logs**: Real-time stdout/stderr from all projects.
-   **Structured Logs**: Searchable table of log entries.
-   **Traces**: Distributed traces showing the flow of requests between frontend, API, and database.
-   **Metrics**: Real-time graphs for CPU, memory, and custom metrics.

## Production Considerations

While Aspire is excellent for local development, it also facilitates deployment. The AppHost can generate manifests for deployment to environments like **Azure Container Apps** or **Kubernetes**.

See the [Aspire Deployment Guide](aspire-deployment-guide.md) for detailed deployment instructions.

## MCP Integration

The Aspire Dashboard exposes a **Model Context Protocol (MCP)** server that provides real-time access to resource logs, traces, and metrics.

### Setup (Recommended)

Run the following command in your AppHost project directory:

```bash
aspire mcp init
```

This will:
1.  Detect your AI environment (VS Code, Copilot CLI, Cursor, etc.).
2.  Create the appropriate configuration file (e.g., `.vscode/mcp.json`).
3.  Generate an `AGENTS.md` file to help AI agents understand your project structure.

### Available Tools

Once connected, your AI assistant can use these tools:
-   **`list_resources`**: List all running services, containers, and executables with their health status and endpoints.
-   **`list_console_logs`**: Stream standard output/error from any resource.
-   **`list_structured_logs`**: Query structured logs with filtering.
-   **`list_traces`**: View distributed traces for request flows.
-   **`execute_resource_command`**: Trigger commands on resources (if supported).
-   **`list_integrations`**: See available hosting integrations.

### Configuration Tips

-   **Exclude Resources**: If you have sensitive resources, you can hide them from the MCP server in your AppHost:
    ```csharp
    builder.AddProject<Projects.SecretService>("secret")
           .ExcludeFromMcp();
    ```
-   **Manual Config**: The server uses `stdio` transport via `aspire mcp start`.

See the [Official Aspire MCP Documentation](https://aspire.dev/get-started/configure-mcp/) for full details.
