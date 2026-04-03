---
name: aspire-azure-storage
description: Add, configure, and integrate Azure Storage (Blobs, Queues, Tables) into an Aspire distributed application — covering AppHost hosting setup (AddAzureStorage, AddBlobs, AddQueues, AddTables, RunAsEmulator/Azurite, WaitFor, WithDataVolume, AsExisting, ConfigureInfrastructure), client integration (AddAzureBlobServiceClient, AddAzureQueueServiceClient, AddAzureTableServiceClient), connection-name matching, connection properties (URI env vars), and the ResourceNames constants pattern. Use this skill whenever the user mentions Azure Storage, Blob Storage, Queue Storage, Table Storage, BlobServiceClient, QueueServiceClient, TableServiceClient, AddAzureStorage, Azurite, Azure storage emulator, or asks how to wire up any Azure storage service in Aspire AppHost or a consuming project — even if they don't use the words "Aspire" or "Azure Storage" explicitly. Prefer this skill over guessing; the shared-storage-account model, RunAsEmulator vs production provisioning, WaitFor ordering, connection-name matching, and the difference between AddBlobs/AddQueues/AddTables all have non-obvious failure modes.
---

# Aspire Azure Storage Skill

Azure Storage in Aspire is built around a **single shared storage account** (`AddAzureStorage`) from which you carve out typed sub-resources for blobs, queues, and tables. Locally, one Azurite container emulates all three services.

## Quick reference

| What you need | See |
|---|---|
| AppHost setup for Blob Storage | [blobs.md](references/blobs.md) |
| AppHost setup for Queue Storage | [queues.md](references/queues.md) |
| AppHost setup for Table Storage | [tables.md](references/tables.md) |

## Core pattern

```csharp
// AppHost.cs — one storage account, one Azurite container
var storage = builder.AddAzureStorage(ResourceNames.Storage)
    .RunAsEmulator();           // Azurite container for local dev

var blobs  = storage.AddBlobs(ResourceNames.Blobs);
var queues = storage.AddQueues(ResourceNames.Queues);
var tables = storage.AddTables(ResourceNames.Tables);

builder.AddProject<Projects.BookStore_ApiService>(ResourceNames.ApiService)
    .WithReference(blobs)
    .WithReference(queues)
    .WithReference(tables)
    .WaitFor(blobs);            // WaitFor any one sub-resource is sufficient
```

```csharp
// Service Program.cs — register clients matching AppHost resource names
builder.AddAzureBlobServiceClient(ResourceNames.Blobs);     // → BlobServiceClient
builder.AddAzureQueueServiceClient(ResourceNames.Queues);   // → QueueServiceClient
builder.AddAzureTableServiceClient(ResourceNames.Tables);   // → TableServiceClient
```

The `connectionName` passed to each `Add*ServiceClient` call **must exactly match** the name given in AppHost. A mismatch produces a silent runtime failure where the service starts but cannot find its connection string.

## ResourceNames pattern

This project uses `ResourceNames` constants (in `BookStore.ServiceDefaults`) instead of raw strings:

```csharp
public static class ResourceNames
{
    public const string Storage = "storage";
    public const string Blobs   = "blobs";
    // Add Queues / Tables as needed
}
```

Always use `ResourceNames.*` — never inline `"blobs"`, `"queues"`, or `"tables"`.

## Current project setup

- AppHost: `AddAzureStorage(ResourceNames.Storage).RunAsEmulator()` → `storage.AddBlobs(ResourceNames.Blobs)`
- API service: `builder.AddAzureBlobServiceClient(ResourceNames.Blobs)` → injects `BlobServiceClient`
- AppHost package: `Aspire.Hosting.Azure.Storage`
- Blob client package: `Aspire.Azure.Storage.Blobs`
- Queue client package: `Aspire.Azure.Storage.Queues`
- Table client package: `Aspire.Azure.Data.Tables`

## Key things that trip people up

- **Package placement**: hosting packages (`Aspire.Hosting.Azure.Storage`) go in AppHost; client packages (`Aspire.Azure.Storage.Blobs`, etc.) go in the consuming project.
- **Sub-resource vs storage account**: `WithReference(storage)` won't work for clients — always `WithReference(blobs/queues/tables)`.
- **No `WaitFor` needed per sub-resource**: wait for one and all three are ready (they share the same Azurite).
- **URI env vars**: `BLOBS_URI`, `QUEUES_URI`, `TABLES_URI` — named `[RESOURCE]_[PROPERTY]` from the AppHost resource name.
- **Production**: drop `RunAsEmulator()` and Aspire provisions a real Azure Storage account via Bicep with RBAC role assignments.
