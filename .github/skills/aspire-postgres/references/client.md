# Aspire PostgreSQL — Raw Npgsql Client Integration

Use this when you want a `NpgsqlDataSource` (or `NpgsqlConnection`) injected via DI — without Entity Framework Core.

For EF Core, see [efcore.md](efcore.md). For Marten, see the [jasperfx-marten skill](../../jasperfx-marten/SKILL.md).

## Package

Add `Aspire.Npgsql` to the consuming service project:

```xml
<!-- BookStore.ApiService.csproj -->
<PackageReference Include="Aspire.Npgsql" />
```

## Register the data source

```csharp
// Program.cs
builder.AddNpgsqlDataSource(ResourceNames.BookStoreDb);
```

The `connectionName` must exactly match the name given to `postgres.AddDatabase(...)` in AppHost.

Aspire automatically resolves the connection string from `ConnectionStrings__bookstore` (injected by `WithReference`) and registers `NpgsqlDataSource` in the DI container with health checks, logging, tracing (`Npgsql` activity source), and metrics.

## Consume via DI

```csharp
public class BookRepository(NpgsqlDataSource dataSource)
{
    public async Task<Book?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT ... FROM books WHERE id = @id";
        cmd.Parameters.AddWithValue("id", id);
        // ...
    }
}
```

## Multiple databases (keyed registration)

When your service connects to more than one database:

```csharp
// Program.cs
builder.AddKeyedNpgsqlDataSource(ResourceNames.BookStoreDb);
builder.AddKeyedNpgsqlDataSource(ResourceNames.AnalyticsDb);

// Service
public class MyService(
    [FromKeyedServices(ResourceNames.BookStoreDb)] NpgsqlDataSource mainDb,
    [FromKeyedServices(ResourceNames.AnalyticsDb)] NpgsqlDataSource analyticsDb)
{ ... }
```

## Configuration options

Override settings inline or via `appsettings.json`:

```csharp
// Inline
builder.AddNpgsqlDataSource(
    ResourceNames.BookStoreDb,
    static settings => settings.DisableHealthChecks = true);
```

```json
// appsettings.json — config key: Aspire:Npgsql
{
  "Aspire": {
    "Npgsql": {
      "ConnectionString": "Host=myserver;Database=bookstore",
      "DisableHealthChecks": false,
      "DisableTracing": false,
      "DisableMetrics": false
    }
  }
}
```

## What's automatically wired up

| Feature | Default | Override key |
|---------|---------|--------------|
| Health check (`NpgSqlHealthCheck`) | ✅ on | `DisableHealthChecks` |
| OpenTelemetry tracing (`Npgsql` source) | ✅ on | `DisableTracing` |
| OpenTelemetry metrics (Npgsql counters) | ✅ on | `DisableMetrics` |
| Logging (`Npgsql.*` categories) | ✅ on | standard `Logging` config |
