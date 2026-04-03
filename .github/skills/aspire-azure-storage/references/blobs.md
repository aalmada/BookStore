# Aspire Azure Blob Storage

## Packages

| Project | Package |
|---------|---------|
| AppHost | `Aspire.Hosting.Azure.Storage` |
| Consuming service | `Aspire.Azure.Storage.Blobs` |

```bash
# AppHost
aspire add azure-storage

# Consuming service
dotnet add package Aspire.Azure.Storage.Blobs
```

## AppHost setup

```csharp
// AppHost.cs
var storage = builder.AddAzureStorage(ResourceNames.Storage)
    .RunAsEmulator();   // Azurite container for local dev; omit for real Azure

var blobs = storage.AddBlobs(ResourceNames.Blobs);

builder.AddProject<Projects.BookStore_ApiService>(ResourceNames.ApiService)
    .WithReference(blobs)
    .WaitFor(blobs);
```

`AddAzureStorage` implicitly calls `AddAzureProvisioning`. When deploying to Azure, it generates a Bicep storage account with Standard GRS, TLS 1.2, and role assignments (`Storage Blob Data Contributor`) for the project identity.

## Azurite emulator configuration

```csharp
// All options are optional
var storage = builder.AddAzureStorage(ResourceNames.Storage)
    .RunAsEmulator(azurite =>
    {
        azurite.WithDataVolume();                    // persist data across restarts
        azurite.WithLifetime(ContainerLifetime.Persistent);  // keep container alive
        // Fixed ports (optional — dynamic by default)
        azurite.WithBlobPort(27000)
               .WithQueuePort(27001)
               .WithTablePort(27002);
    });
```

Default Azurite ports: blob=10000, queue=10001, table=10002 (mapped to dynamic host ports).

## Connect to existing Azure Storage account

```csharp
var existingStorageName = builder.AddParameter("existingStorageName");
var existingStorageResourceGroup = builder.AddParameter("existingStorageResourceGroup");

var storage = builder.AddAzureStorage(ResourceNames.Storage)
    .AsExisting(existingStorageName, existingStorageResourceGroup)
    .AddBlobs(ResourceNames.Blobs);
```

Requires AppHost config: `SubscriptionId`, `ResourceGroup`, `Location`.

## Customize provisioned infrastructure (production)

```csharp
var storage = builder.AddAzureStorage(ResourceNames.Storage)
    .ConfigureInfrastructure(infra =>
    {
        var account = infra.GetProvisionableResources()
            .OfType<StorageAccount>()
            .Single();
        account.Sku = new StorageSku { Name = StorageSkuName.PremiumLrs };
        account.AccessTier = StorageAccountAccessTier.Premium;
        account.Tags.Add("environment", "production");
    });
```

## Client setup (consuming project)

```csharp
// Program.cs
builder.AddAzureBlobServiceClient(ResourceNames.Blobs);
```

Injects `BlobServiceClient` via dependency injection.

```csharp
// Service
public class BlobStorageService(BlobServiceClient blobServiceClient)
{
    public async Task UploadAsync(string containerName, string blobName, Stream data,
        CancellationToken cancellationToken = default)
    {
        var container = blobServiceClient.GetBlobContainerClient(containerName);
        await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        await container.UploadBlobAsync(blobName, data, cancellationToken);
    }
}
```

## Connection properties (environment variables)

| Resource name | Variable | Value |
|---|---|---|
| `blobs` | `BLOBS_URI` | `https://{account}.blob.core.windows.net/` |
| `blobs` | `BLOBS_CONNECTIONSTRING` | emulator only |

Named by convention: `[RESOURCE_UPPERCASE]_[PROPERTY]`.

## Configuration key (`appsettings.json`)

```json
{
  "Aspire": {
    "Azure": {
      "Storage": {
        "Blobs": {
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
builder.AddAzureBlobServiceClient(ResourceNames.Blobs,
    settings => settings.DisableHealthChecks = true);
```

## Observability

- **Logs**: `Azure.Core`, `Azure.Identity`
- **Traces**: `Azure.Storage.Blobs.BlobContainerClient`
- **Metrics**: not available (Azure SDK limitation)

## Common mistakes

- `WithReference(storage)` instead of `WithReference(blobs)` → client cannot resolve the connection string
- Missing `WaitFor(blobs)` → service starts before Azurite is ready → transient failures
- Adding `Aspire.Azure.Storage.Blobs` to AppHost (wrong project) → add it to the consuming service instead
