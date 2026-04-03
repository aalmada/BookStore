# Aspire PostgreSQL — EF Core Client Integration

Use this when you want a `DbContext` registered in DI with Aspire observability features.

For raw Npgsql (no EF Core), see [client.md](client.md). For Marten (event sourcing), see the [jasperfx-marten skill](../../jasperfx-marten/SKILL.md).

## Package

Add `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL` to the consuming service project:

```xml
<PackageReference Include="Aspire.Npgsql.EntityFrameworkCore.PostgreSQL" />
```

## Register a new DbContext (recommended)

```csharp
// Program.cs
builder.AddNpgsqlDbContext<AppDbContext>(ResourceNames.BookStoreDb);
```

This single call:
- Reads the connection string from `ConnectionStrings__bookstore`
- Registers `AppDbContext` in DI (scoped)
- Adds a `DbContextHealthCheck` (`CanConnectAsync`)
- Wires Npgsql OpenTelemetry tracing and EF Core metrics

The `connectionName` must exactly match the name given to `postgres.AddDatabase(...)` in AppHost.

## Consume via DI

```csharp
public class BookRepository(AppDbContext db)
{
    public Task<List<Book>> GetAllAsync(CancellationToken ct) =>
        db.Books.AsNoTracking().ToListAsync(ct);
}
```

## Enrich an existing DbContext registration

If you already register the context via `services.AddDbContext<T>` (e.g. to preserve custom options, interceptors, or to avoid EF context pooling), enrich it instead:

```csharp
// Already registered the standard way:
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString(ResourceNames.BookStoreDb)
            ?? throw new InvalidOperationException("Connection string not found")));

// Then add Aspire health checks, retries, logging and telemetry on top:
builder.EnrichNpgsqlDbContext<AppDbContext>(settings =>
{
    settings.DisableRetry = false;
    settings.CommandTimeout = 30;
});
```

`EnrichNpgsqlDbContext` does **not** read from `ConnectionStrings` — it requires the `DbContext` to already be registered.

## Multiple DbContext classes

Register each context with its own connection name:

```csharp
builder.AddNpgsqlDbContext<AppDbContext>(ResourceNames.BookStoreDb);
builder.AddNpgsqlDbContext<AnalyticsDbContext>(ResourceNames.AnalyticsDb);
```

To configure each context separately via `appsettings.json`, use the context type name as a sub-key:

```json
{
  "Aspire": {
    "Npgsql": {
      "EntityFrameworkCore": {
        "PostgreSQL": {
          "ConnectionString": "...",
          "AppDbContext": {
            "DisableRetry": false
          },
          "AnalyticsDbContext": {
            "ConnectionString": "...",
            "DisableTracing": true
          }
        }
      }
    }
  }
}
```

## Configuration options

```csharp
// Inline delegate
builder.AddNpgsqlDbContext<AppDbContext>(
    ResourceNames.BookStoreDb,
    static settings =>
    {
        settings.DisableRetry = false;
        settings.CommandTimeout = 30;
    });
```

```json
// appsettings.json — config key: Aspire:Npgsql:EntityFrameworkCore:PostgreSQL
{
  "Aspire": {
    "Npgsql": {
      "EntityFrameworkCore": {
        "PostgreSQL": {
          "DisableHealthChecks": false,
          "DisableTracing": false,
          "DisableRetry": false,
          "CommandTimeout": 30
        }
      }
    }
  }
}
```

## What's automatically wired up

| Feature | Default | Override key |
|---------|---------|--------------|
| `DbContextHealthCheck` (`CanConnectAsync`) | ✅ on | `DisableHealthChecks` |
| Retry on transient failures | ✅ on | `DisableRetry` |
| OpenTelemetry tracing (`Npgsql` source) | ✅ on | `DisableTracing` |
| EF Core metrics (`ec_Microsoft_EntityFrameworkCore_*`) | ✅ on | `DisableMetrics` |
| EF Core + Npgsql log categories | ✅ on | standard `Logging` config |

## Migrations

EF Core migrations work as normal. For Aspire-integrated migration execution at startup, see [Apply migrations](https://aspire.dev/integrations/databases/efcore/migrations/).
