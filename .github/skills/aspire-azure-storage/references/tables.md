# Aspire Azure Table Storage

## Packages

| Project | Package |
|---------|---------|
| AppHost | `Aspire.Hosting.Azure.Storage` |
| Consuming service | `Aspire.Azure.Data.Tables` |

```bash
# AppHost
aspire add azure-storage

# Consuming service
dotnet add package Aspire.Azure.Data.Tables
```

> Note: the client package is `Aspire.Azure.Data.Tables`, not `Aspire.Azure.Storage.Tables`. The namespace follows the Azure SDK naming convention for Tables.

## AppHost setup

```csharp
// AppHost.cs
var storage = builder.AddAzureStorage(ResourceNames.Storage)
    .RunAsEmulator();   // Azurite container for local dev; omit for real Azure

var tables = storage.AddTables(ResourceNames.Tables);

builder.AddProject<Projects.BookStore_ApiService>(ResourceNames.ApiService)
    .WithReference(tables)
    .WaitFor(tables);
```

## Combined setup (all three on same account)

```csharp
var storage = builder.AddAzureStorage(ResourceNames.Storage)
    .RunAsEmulator();

var blobs  = storage.AddBlobs(ResourceNames.Blobs);
var queues = storage.AddQueues(ResourceNames.Queues);
var tables = storage.AddTables(ResourceNames.Tables);

builder.AddProject<Projects.BookStore_ApiService>(ResourceNames.ApiService)
    .WithReference(blobs)
    .WithReference(queues)
    .WithReference(tables)
    .WaitFor(blobs);    // One WaitFor is sufficient
```

## Client setup (consuming project)

```csharp
// Program.cs
builder.AddAzureTableServiceClient(ResourceNames.Tables);
```

Injects `TableServiceClient` via dependency injection.

```csharp
// Service
public class TableStorageService(TableServiceClient tableServiceClient)
{
    public async Task UpsertEntityAsync<T>(string tableName, T entity,
        CancellationToken cancellationToken = default) where T : ITableEntity
    {
        var table = tableServiceClient.GetTableClient(tableName);
        await table.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        await table.UpsertEntityAsync(entity, TableUpdateMode.Merge, cancellationToken);
    }
}
```

## Connection properties (environment variables)

| Resource name | Variable | Value |
|---|---|---|
| `tables` | `TABLES_URI` | `https://{account}.table.core.windows.net/` |
| `tables` | `TABLES_CONNECTIONSTRING` | emulator only |

Named by convention: `[RESOURCE_UPPERCASE]_[PROPERTY]`.

## Configuration key (`appsettings.json`)

```json
{
  "Aspire": {
    "Azure": {
      "Data": {
        "Tables": {
          "DisableHealthChecks": false,
          "DisableTracing": false
        }
      }
    }
  }
}
```

Note: Tables uses `Aspire:Azure:Data:Tables` (not `Storage:Tables`) — this differs from Blobs and Queues.

Override inline:
```csharp
builder.AddAzureTableServiceClient(ResourceNames.Tables,
    settings => settings.DisableHealthChecks = true);
```

Or configure `TableClientOptions`:
```csharp
builder.AddAzureTableServiceClient(ResourceNames.Tables,
    configureClientBuilder: cb => cb.ConfigureOptions(
        opts => opts.EnableTenantDiscovery = true));
```

## Observability

- **Logs**: `Azure.Core`, `Azure.Identity`
- **Traces**: `Azure.Data.Tables.TableServiceClient`
- **Metrics**: not available (Azure SDK limitation)

## Common mistakes

- Using `Aspire.Azure.Storage.Tables` (does not exist) instead of `Aspire.Azure.Data.Tables`
- Configuration key confusion: Tables uses `Aspire:Azure:Data:Tables`; Blobs uses `Aspire:Azure:Storage:Blobs`; Queues uses `Aspire:Azure:Storage:Queues`
- `TableServiceClient` is for account-level operations; use `tableServiceClient.GetTableClient(tableName)` to interact with a specific table
