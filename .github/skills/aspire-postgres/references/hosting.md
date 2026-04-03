# Aspire PostgreSQL — Hosting Integration (AppHost)

## Package

Add `Aspire.Hosting.PostgreSQL` to your AppHost project:

```xml
<!-- BookStore.AppHost.csproj -->
<PackageReference Include="Aspire.Hosting.PostgreSQL" />
```

Or via the CLI:
```bash
aspire add postgresql
```

## Add a server + database resource

```csharp
// AppHost.cs
var postgres = builder.AddPostgres(ResourceNames.Postgres);
var bookStoreDb = postgres.AddDatabase(ResourceNames.BookStoreDb);

builder.AddProject<Projects.BookStore_ApiService>(ResourceNames.ApiService)
    .WithReference(bookStoreDb)   // injects ConnectionStrings__bookstore into the service
    .WaitFor(postgres);            // prevents boot race — service waits for server health check
```

**Key points:**
- `.AddPostgres(name)` creates a `PostgresServerResource` (the container).
- `.AddDatabase(name)` creates a `PostgresDatabaseResource` (the logical DB). The database is created automatically when the server becomes ready, via `ResourceReadyEvent`.
- Always pass the **database** resource to `.WithReference()`, not the server — only the database resource injects a full `ConnectionStrings__<name>` variable including the database name.
- Default credentials: username `postgres`, randomly generated password per run. Use `AddParameter` if you need fixed credentials (see below).

## Connect to an existing PostgreSQL server

```csharp
var postgres = builder.AddPostgres(ResourceNames.Postgres)
    .AsExisting();
```

Provide the connection string in `appsettings.json` or user secrets:
```json
{
  "ConnectionStrings": {
    "postgres": "Host=myserver;Port=5432;Username=myuser;Password=mypassword"
  }
}
```

## Data persistence (survive container restarts)

By default the postgres container is ephemeral. To persist data across restarts:

**Data volume** (recommended for local dev):
```csharp
var postgres = builder.AddPostgres(ResourceNames.Postgres)
    .WithDataVolume();   // mounts /var/lib/postgresql/data
```

**Bind mount** (maps a host directory):
```csharp
var postgres = builder.AddPostgres(ResourceNames.Postgres)
    .WithDataBindMount(source: "/PostgreSQL/Data");
```

> **Azure Container Apps caveat**: Data volumes cannot be used with ACA (SMB limitation — the container gets stuck in *Activating* state). For production on Azure, use Azure Database for PostgreSQL Flexible Server instead.

## Init scripts

Seed the server on first start with SQL or shell scripts:

```csharp
var postgres = builder.AddPostgres(ResourceNames.Postgres)
    .WithInitBindMount(@"/path/to/init/scripts");  // folder of .sql / .sh files
```

## Custom creation script per database

Override the default `CREATE DATABASE` script:

```csharp
var db = postgres.AddDatabase("app_db")
    .WithCreationScript("""
        CREATE DATABASE app_db;
        -- additional init SQL here
        """);
```

Note: `\c` (connect to database) is not supported inside creation scripts.

## Management UIs

**pgAdmin** (official web GUI, from `dpage/pgadmin4`):
```csharp
var postgres = builder.AddPostgres(ResourceNames.Postgres)
    .WithPgAdmin();                           // randomly assigned port
    // or: .WithPgAdmin(p => p.WithHostPort(5050))
```

**pgWeb** (lightweight read-only GUI, from `sosedoff/pgweb`):
```csharp
var postgres = builder.AddPostgres(ResourceNames.Postgres)
    .WithPgWeb();
```

**Community Toolkit extras** (`CommunityToolkit.Aspire.Hosting.PostgreSQL.Extensions`):
```csharp
// DbGate — comprehensive multi-database manager
var postgres = builder.AddPostgres(ResourceNames.Postgres)
    .WithDbGate();

// Adminer — lightweight single-page UI
var postgres = builder.AddPostgres(ResourceNames.Postgres)
    .WithAdminer();
```

## Explicit credentials (for reproducible local dev or CI)

```csharp
var username = builder.AddParameter("pg-username", secret: true);
var password = builder.AddParameter("pg-password", secret: true);

var postgres = builder.AddPostgres(ResourceNames.Postgres, username, password);
```

Provide values in `appsettings.json` or user secrets:
```json
{
  "Parameters": {
    "pg-username": "myuser",
    "pg-password": "mypassword"
  }
}
```

## Health checks

The hosting integration automatically registers an Npgsql health check for the server resource. The service project's `/health` endpoint will not report healthy until the PostgreSQL server passes.

## Environment variables injected by WithReference

When a service calls `.WithReference(bookStoreDb)`, Aspire injects:

| Variable | Example value |
|----------|--------------|
| `ConnectionStrings__bookstore` | `Host=localhost;Port=5432;Database=bookstore;Username=postgres;Password=...` |
| `BOOKSTORE_HOST` | `localhost` |
| `BOOKSTORE_PORT` | `5432` |
| `BOOKSTORE_USERNAME` | `postgres` |
| `BOOKSTORE_PASSWORD` | `...` |
| `BOOKSTORE_DATABASENAME` | `bookstore` |

The `ConnectionStrings__<name>` variable is the one used by `configuration.GetConnectionString(name)` in the service.
