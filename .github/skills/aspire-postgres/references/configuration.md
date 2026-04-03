# Aspire PostgreSQL — Configuration, Health Checks & Telemetry

## Connection string resolution

Aspire resolves connection strings from environment variables injected by `WithReference`. In the consuming service:

```csharp
// All of these resolve from ConnectionStrings__bookstore:
builder.AddNpgsqlDataSource(ResourceNames.BookStoreDb);
builder.AddNpgsqlDbContext<AppDbContext>(ResourceNames.BookStoreDb);
configuration.GetConnectionString(ResourceNames.BookStoreDb);  // Marten / manual
```

You can also provide a connection string directly in `appsettings.json` (useful for running outside Aspire):

```json
{
  "ConnectionStrings": {
    "bookstore": "Host=localhost;Port=5432;Database=bookstore;Username=postgres;Password=secret"
  }
}
```

## Health checks

Both the hosting and client integrations register health checks automatically:

| Layer | Health check | Endpoint |
|-------|-------------|---------|
| Hosting (`Aspire.Hosting.PostgreSQL`) | Server reachability via `AspNetCore.HealthChecks.Npgsql` | Aspire dashboard |
| Client (`Aspire.Npgsql`) | `NpgSqlHealthCheck` — runs a command against the database | `/health` |
| EF Core (`Aspire.Npgsql.EFCore.PostgreSQL`) | `DbContextHealthCheck` — calls `CanConnectAsync` | `/health` |

Suppress in the client integrations if needed:

```csharp
builder.AddNpgsqlDataSource(ResourceNames.BookStoreDb,
    static s => s.DisableHealthChecks = true);

builder.AddNpgsqlDbContext<AppDbContext>(ResourceNames.BookStoreDb,
    static s => s.DisableHealthChecks = true);
```

## OpenTelemetry tracing

Both `Aspire.Npgsql` and `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL` wire the `Npgsql` activity source:

```csharp
// Disable if too noisy:
builder.AddNpgsqlDataSource(ResourceNames.BookStoreDb,
    static s => s.DisableTracing = true);
```

## Log categories

**Raw Npgsql:**
- `Npgsql.Connection`, `Npgsql.Command`, `Npgsql.Transaction`, `Npgsql.Copy`, `Npgsql.Replication`, `Npgsql.Exception`

**EF Core (additional):**
- `Microsoft.EntityFrameworkCore.Database.Command`, `Microsoft.EntityFrameworkCore.Database.Connection`, `Microsoft.EntityFrameworkCore.Migrations`, `Microsoft.EntityFrameworkCore.Query`, `Microsoft.EntityFrameworkCore.Update`, *and others*

Set verbosity in `appsettings.Development.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Npgsql": "Warning",
      "Microsoft.EntityFrameworkCore.Database.Command": "Information"
    }
  }
}
```

## Configuration keys (appsettings.json)

| Package | Config section |
|---------|---------------|
| `Aspire.Npgsql` | `Aspire:Npgsql` |
| `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL` | `Aspire:Npgsql:EntityFrameworkCore:PostgreSQL` |
| Per-DbContext type | `Aspire:Npgsql:EntityFrameworkCore:PostgreSQL:{TypeName}` |

## NpgsqlSettings (Aspire.Npgsql)

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ConnectionString` | `string?` | from env | Override connection string |
| `DisableHealthChecks` | `bool` | `false` | Skip health check registration |
| `DisableTracing` | `bool` | `false` | Skip OpenTelemetry tracing |
| `DisableMetrics` | `bool` | `false` | Skip OpenTelemetry metrics |

## NpgsqlEntityFrameworkCorePostgreSQLSettings

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ConnectionString` | `string?` | from env | Override connection string |
| `DisableHealthChecks` | `bool` | `false` | Skip health check registration |
| `DisableTracing` | `bool` | `false` | Skip OpenTelemetry tracing |
| `DisableMetrics` | `bool` | `false` | Skip OpenTelemetry metrics |
| `DisableRetry` | `bool` | `false` | Disable Npgsql retry-on-failure |
| `CommandTimeout` | `int?` | null | SQL command timeout in seconds |
