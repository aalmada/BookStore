# Aspire Azure Queue Storage

## Packages

| Project | Package |
|---------|---------|
| AppHost | `Aspire.Hosting.Azure.Storage` |
| Consuming service | `Aspire.Azure.Storage.Queues` |

```bash
# AppHost
aspire add azure-storage

# Consuming service
dotnet add package Aspire.Azure.Storage.Queues
```

## AppHost setup

```csharp
// AppHost.cs
var storage = builder.AddAzureStorage(ResourceNames.Storage)
    .RunAsEmulator();   // Azurite container for local dev; omit for real Azure

var queues = storage.AddQueues(ResourceNames.Queues);

builder.AddProject<Projects.BookStore_ApiService>(ResourceNames.ApiService)
    .WithReference(queues)
    .WaitFor(queues);
```

The same `AddAzureStorage` storage account hosts blobs, queues, and tables — you only call it once per account. One Azurite container emulates all three.

## Combined setup (blobs + queues on same account)

```csharp
var storage = builder.AddAzureStorage(ResourceNames.Storage)
    .RunAsEmulator();

var blobs  = storage.AddBlobs(ResourceNames.Blobs);
var queues = storage.AddQueues(ResourceNames.Queues);

builder.AddProject<Projects.BookStore_ApiService>(ResourceNames.ApiService)
    .WithReference(blobs)
    .WithReference(queues)
    .WaitFor(blobs);    // One WaitFor is enough — same Azurite container
```

## Client setup (consuming project)

```csharp
// Program.cs
builder.AddAzureQueueServiceClient(ResourceNames.Queues);
```

Injects `QueueServiceClient` via dependency injection.

```csharp
// Service
public class QueueSenderService(QueueServiceClient queueServiceClient)
{
    public async Task EnqueueAsync(string queueName, string message,
        CancellationToken cancellationToken = default)
    {
        var queue = queueServiceClient.GetQueueClient(queueName);
        await queue.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        await queue.SendMessageAsync(message, cancellationToken);
    }
}
```

## Connection properties (environment variables)

| Resource name | Variable | Value |
|---|---|---|
| `queues` | `QUEUES_URI` | `https://{account}.queue.core.windows.net/` |
| `queues` | `QUEUES_CONNECTIONSTRING` | emulator only |

Named by convention: `[RESOURCE_UPPERCASE]_[PROPERTY]`.

## Configuration key (`appsettings.json`)

```json
{
  "Aspire": {
    "Azure": {
      "Storage": {
        "Queues": {
          "DisableHealthChecks": false,
          "DisableTracing": false
        }
      }
    }
  }
}
```

Override inline:
```csharp
builder.AddAzureQueueServiceClient(ResourceNames.Queues,
    settings => settings.DisableHealthChecks = true);
```

## Observability

- **Logs**: `Azure.Core`, `Azure.Identity`
- **Traces**: `Azure.Storage.Queues.QueueClient`
- **Metrics**: not available (Azure SDK limitation)

## Common mistakes

- `WithReference(storage)` instead of `WithReference(queues)` → client cannot resolve the connection string; always reference the sub-resource
- Missing `WaitFor` → service attempts to connect before Azurite is initialised
- Calling `AddAzureStorage` a second time for queues → creates a second storage account; add queues to the existing `IResourceBuilder<IAzureStorageResource>` returned by the first call
