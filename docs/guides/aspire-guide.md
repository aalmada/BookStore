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

The `BookStore.AppHost` project is the entry point for the distributed application. It defines the architectural blueprint of the system in C#.

### Defined Resources

The AppHost defines the following resources in `Program.cs`:

#### 1. Databases & Storage
-   **PostgreSQL**: A containerized PostgreSQL instance.
    ```csharp
    var postgres = builder.AddPostgres("postgres")
        .WithPgAdmin(); // Adds PgAdmin for database management
    var bookStoreDb = postgres.AddDatabase("bookstore");
    ```
-   **Redis**: Distributed cache for the API and hybrid caching.
    ```csharp
    var cache = builder.AddRedis("cache");
    ```
-   **Azure Storage**: Uses the **Azurite** emulator for local development, providing Blob storage compatible with Azure.
    ```csharp
    var storage = builder.AddAzureStorage("storage").RunAsEmulator();
    var blobs = storage.AddBlobs("blobs");
    ```

#### 2. Services
-   **API Service** (`Projects.BookStore_ApiService`): The backend API. It declares dependencies on the database, blob storage, and cache.
    ```csharp
    var apiService = builder.AddProject<Projects.BookStore_ApiService>("apiservice")
        .WithReference(bookStoreDb)
        .WithReference(blobs)
        .WithReference(cache)
        .WaitFor(postgres); // Startup dependency
    ```
-   **Web Frontend** (`Projects.BookStore_Web`): The Blazor app. It depends on the API service to fetch data.
    ```csharp
    builder.AddProject<Projects.BookStore_Web>("webfrontend")
        .WithReference(apiService)
        .WaitFor(apiService);
    ```

### Service Discovery

Aspire automates connection management using the resource names defined in the AppHost.
-   **Databases**: The API service receives the connection string for "bookstore" automatically.
-   **HTTP Services**: The frontend receives the base URL for "apiservice" via the service discovery mechanism, allowing `HttpClient` to simply use `http://apiservice`.

## Service Defaults: Shared Concerns

The `BookStore.ServiceDefaults` project encapsulates cross-cutting concerns that every service needs. Both the API and Web projects reference this library and call `builder.AddServiceDefaults()` in their startup.

### Features
-   **OpenTelemetry**: Automatically configures logging, tracing, and metrics export to the Aspire Dashboard.
-   **Health Checks**: Adds standard `/health` and `/alive` endpoints.
-   **Service Discovery**: Configures `Microsoft.Extensions.ServiceDiscovery` to resolve Aspire resource names.

## Local Development Experience

To start the entire solution, simply run the AppHost project:

```bash
aspire run
# or
dotnet run --project src/BookStore.AppHost/BookStore.AppHost.csproj
```

### The Aspire Dashboard

When the application starts, the **Aspire Dashboard** launches automatically. It provides a centralized view of:
-   **Resources**: Status and endpoints of all running services and containers.
-   **Console Logs**: Real-time stdout/stderr from all projects.
-   **Structured Logs**: searchable table of log entries.
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
