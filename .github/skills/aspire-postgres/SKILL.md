---
name: aspire-postgres
description: Add, configure, and integrate PostgreSQL into an Aspire distributed application — covering AppHost hosting setup (AddPostgres, AddDatabase, WaitFor, WithPgAdmin, WithDataVolume, WithCreationScript), client integration (AddNpgsqlDataSource, AddNpgsqlDbContext, AddKeyedNpgsqlDataSource, EnrichNpgsqlDbContext), connection string resolution, and the ResourceNames constants pattern. Use this skill whenever the user mentions PostgreSQL, Npgsql, EF Core with Postgres, AddPostgres, AddDatabase, connection strings in Aspire, or asks how to wire up a database in Aspire AppHost or a consuming service — even if they don't use the words "Aspire" or "PostgreSQL" explicitly. Prefer this skill over guessing; the server-vs-database resource split, WaitFor ordering, connectionName matching, Marten vs EF Core vs raw Npgsql client choice, and data volume ACA limitations all have non-obvious failure modes.
---

# Aspire PostgreSQL Skill

PostgreSQL in Aspire is split across two concerns:

- **Hosting** (AppHost) — declare the server and database resources, wire them to services, configure persistence/tooling
- **Client** (your service) — register the Npgsql client or DbContext by referencing the named resource

## Quick reference

| Topic | See |
|-------|-----|
| `AddPostgres`, `AddDatabase`, `WithReference`, `WaitFor`, volumes, pgAdmin/pgWeb, parameters, scripts | [hosting.md](references/hosting.md) |
| `AddNpgsqlDataSource`, `AddKeyedNpgsqlDataSource`, raw Npgsql without EF Core | [client.md](references/client.md) |
| `AddNpgsqlDbContext`, `EnrichNpgsqlDbContext`, EF Core integration | [efcore.md](references/efcore.md) |
| Health checks, telemetry, config keys, appsettings | [configuration.md](references/configuration.md) |

## Core pattern

```csharp
// AppHost.cs
var postgres = builder.AddPostgres(ResourceNames.Postgres)
    .WithPgAdmin();                     // dev-only admin UI

var bookStoreDb = postgres.AddDatabase(ResourceNames.BookStoreDb);

builder.AddProject<Projects.BookStore_ApiService>(ResourceNames.ApiService)
    .WithReference(bookStoreDb)         // injects ConnectionStrings__bookstore env var
    .WaitFor(postgres);                 // wait for the server to be healthy before starting

// Service Program.cs — raw Npgsql
builder.AddNpgsqlDataSource(ResourceNames.BookStoreDb);

// — OR — EF Core
builder.AddNpgsqlDbContext<AppDbContext>(ResourceNames.BookStoreDb);

// — OR — Marten (manually resolve the connection string)
var connectionString = configuration.GetConnectionString(ResourceNames.BookStoreDb);
```

The `connectionName` / `GetConnectionString` key must **exactly** match the name given to `postgres.AddDatabase(...)`.

## ResourceNames pattern

Always use `ResourceNames.*` constants (from `BookStore.ServiceDefaults`) — never hardcode strings:

```csharp
// BookStore.ServiceDefaults/ResourceNames.cs
public static class ResourceNames
{
    public const string Postgres    = "postgres";
    public const string BookStoreDb = "bookstore";
    // ...
}
```

## Current project setup

| Layer | Detail |
|-------|--------|
| AppHost package | `Aspire.Hosting.PostgreSQL` |
| AppHost setup | `builder.AddPostgres(ResourceNames.Postgres).WithPgAdmin()` + `postgres.AddDatabase(ResourceNames.BookStoreDb)` |
| API service | Uses **Marten** — resolves `configuration.GetConnectionString(ResourceNames.BookStoreDb)` directly (no `Aspire.Npgsql` package) |
| `.WithDataVolume()` | Commented out in AppHost — uncomment for persistent local dev data |

See `src/BookStore.AppHost/AppHost.cs` and `src/BookStore.ApiService/Infrastructure/Extensions/MartenConfigurationExtensions.cs`.

## Common mistakes

- **Name mismatch**: `postgres.AddDatabase("bookstore")` in AppHost but `GetConnectionString("db")` in service → connection string is null at startup
- **WaitFor the server, not just the database**: use `.WaitFor(postgres)` on the project; the database resource is created automatically when the server is ready
- **WithReference on the database resource**: always pass `bookStoreDb` (the `PostgresDatabaseResource`), not `postgres` (the `PostgresServerResource`) — the former injects the full connection string including database name
- **Data volumes and Azure Container Apps**: `.WithDataVolume()` works locally but fails in ACA (SMB limitation); use Azure Database for PostgreSQL Flexible Server for deployment
- **Using `AddNpgsqlDbContext` vs `EnrichNpgsqlDbContext`**: use `AddNpgsqlDbContext` for new registrations; use `EnrichNpgsqlDbContext` when you already called `services.AddDbContext<T>` and just want Aspire observability on top
- **Wrong package in wrong project**: `Aspire.Hosting.PostgreSQL` goes in AppHost; `Aspire.Npgsql` / `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL` go in the consuming service
