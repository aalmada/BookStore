# AppHost Instructions

**Scope**: `src/BookStore.AppHost/**`

## Core Rules
- **Aspire**: Use Aspire for all resource orchestration and service discovery.
- **Resource Definition**: Define all resources (API, Web, PostgreSQL, Redis, etc.) in `Program.cs`.
- **Environment Variables**: Manage configuration and connection strings centrally.
- **Service Discovery**: Use Aspire's built-in service discovery for inter-service communication.

## Resource Configuration Patterns

### Database Resources
```csharp
// PostgreSQL with PgAdmin
var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin()
    .AddDatabase("bookstoredb");
```

### Redis Cache
```csharp
var redis = builder.AddRedis("cache")
    .WithRedisCommander();  // Optional: UI for Redis
```

### API Service
```csharp
var apiService = builder.AddProject<Projects.BookStore_ApiService>("apiservice")
    .WithReference(postgres)
    .WithReference(redis)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment);
```

### Web Frontend
```csharp
builder.AddProject<Projects.BookStore_Web>("webfrontend")
    .WithReference(apiService)  // Service discovery
    .WithExternalHttpEndpoints();
```

## Service Discovery

Aspire automatically configures service discovery. In the Web project, you can reference the API via:

```csharp
// ServiceDefaults automatically configures HttpClient discovery
builder.Services.AddHttpServiceReference<IBooksClient>("apiservice");
```

## Environment Variables

### Setting Environment Variables
```csharp
var apiService = builder.AddProject<Projects.BookStore_ApiService>("apiservice")
    .WithEnvironment("ConnectionStrings__Default", postgres)
    .WithEnvironment("Authentication__JwtKey", builder.Configuration["JwtKey"])
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment);
```

### Using Configuration
```csharp
// Access AppHost configuration
var jwtKey = builder.Configuration["JwtKey"];
var isDevelopment = builder.Environment.IsDevelopment();
```

## Connection Strings

Aspire handles connection strings automatically:

```csharp
// AppHost
var postgres = builder.AddPostgres("postgres")
    .AddDatabase("bookstoredb");

var apiService = builder.AddProject<Projects.BookStore_ApiService>("apiservice")
    .WithReference(postgres);  // Injects ConnectionStrings__bookstoredb

// ApiService (automatic)
// Connection string is available as "bookstoredb" via configuration
builder.Configuration.GetConnectionString("bookstoredb");
```

## Common Resource Types

### Azure Services (Deployment)
```csharp
// Azure SQL Database
var sql = builder.AddAzureSqlServer("sql")
    .AddDatabase("bookstoredb");

// Azure Redis Cache
var redis = builder.AddAzureRedis("cache");

// Azure Storage
var storage = builder.AddAzureStorage("storage")
    .AddBlobs("blobs");
```

## Health Checks

Resources automatically expose health checks:
- `http://localhost:17161/health` - Aspire dashboard health endpoint
- Individual service health endpoints are aggregated

## Development vs. Production

```csharp
if (builder.Environment.IsDevelopment())
{
    // Use local PostgreSQL container
    var postgres = builder.AddPostgres("postgres");
}
else
{
    // Use Azure SQL in production
    var sql = builder.AddAzureSqlServer("sql");
}
```

## Troubleshooting

### Port Conflicts
If ports are already in use, Aspire will auto-assign new ones. Check the dashboard for actual URLs.

### Container Startup
Ensure Docker Desktop is running before executing `aspire run`.

### Resource Dependencies
Ensure resources are defined before they're referenced:
```csharp
// ✅ Correct order
var postgres = builder.AddPostgres("postgres");
var apiService = builder.AddProject<Projects.BookStore_ApiService>("apiservice")
    .WithReference(postgres);

// ❌ Wrong - postgres not defined yet
var apiService = builder.AddProject<Projects.BookStore_ApiService>("apiservice")
    .WithReference(postgres);
var postgres = builder.AddPostgres("postgres");
```

## References
- See [Aspire Orchestration Guide](../../docs/aspire-guide.md) for detailed patterns
- See [Aspire Deployment Guide](../../docs/aspire-deployment-guide.md) for Azure and Kubernetes deployment

