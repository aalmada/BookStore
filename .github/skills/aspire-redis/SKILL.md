---
name: aspire-redis
description: Add, configure, and integrate Redis into an Aspire distributed application — covering AppHost hosting setup (AddRedis, WaitFor, data volumes, persistence, Redis Insight/Commander), client integration (AddRedisClient, AddRedisDistributedCache, AddKeyedRedisClient, AddRedisClientBuilder), Azure authentication, and the ResourceNames constants pattern. Use this skill whenever the user mentions Redis, StackExchange.Redis, distributed cache, AddRedis, cache resource, IConnectionMultiplexer, or asks how to wire up Redis in Aspire AppHost or a client project — even if they don't use the words "Aspire" or "Redis" explicitly. Prefer this skill over guessing; the AppHost-vs-client split, WaitFor ordering, DistributedCaching vs raw client choice, and connection-name matching all have non-obvious failure modes.
---

# Aspire Redis Skill

Redis in Aspire is split across two concerns:

- **Hosting** (AppHost) — declare the Redis resource, pass it to services, configure persistence/tooling
- **Client** (your service/API) — register the Redis client or distributed cache by referencing the named resource

## Quick reference

| Topic | See |
|-------|-----|
| `AddRedis`, `WithReference`, `WaitFor`, volumes, persistence, tools | [hosting.md](references/hosting.md) |
| `AddRedisDistributedCache`, `AddRedisClient`, `AddKeyedRedisClient`, builder pattern | [client.md](references/client.md) |
| Configuration, health checks, telemetry, Azure auth | [configuration.md](references/configuration.md) |

## Core pattern

```csharp
// AppHost.cs
var cache = builder.AddRedis(ResourceNames.Cache);

builder.AddProject<Projects.BookStore_ApiService>(ResourceNames.ApiService)
    .WithReference(cache)   // injects ConnectionStrings__cache env var
    .WaitFor(cache);        // waits for Redis to be healthy before starting

// API Program.cs — use as L2 for HybridCache
builder.AddRedisDistributedCache(ResourceNames.Cache);

// OR — use raw IConnectionMultiplexer
builder.AddRedisClient(ResourceNames.Cache);
```

The `connectionName` passed to `AddRedisDistributedCache` / `AddRedisClient` must exactly match the name given to `AddRedis` in AppHost.

## ResourceNames pattern

This project uses a shared `ResourceNames` constants class (in `BookStore.ServiceDefaults`) to avoid magic strings:

```csharp
// BookStore.ServiceDefaults/ResourceNames.cs
public static class ResourceNames
{
    public const string Cache = "cache";
    // ...
}
```

Always use `ResourceNames.*` constants rather than inline strings like `"cache"`.

## Current project setup

- AppHost: `builder.AddRedis(ResourceNames.Cache)` — runs the official `docker.io/library/redis` container locally
- API service: `builder.AddRedisDistributedCache(ResourceNames.Cache)` — registers `IDistributedCache`, which `HybridCache` uses as its L2 tier
- Package in AppHost (`Aspire.Hosting.Redis`), Package in API (`Aspire.StackExchange.Redis.DistributedCaching`)
- See the [aspnet-hybrid-cache](../aspnet-hybrid-cache/SKILL.md) skill for HybridCache usage patterns on top of this setup

## Common mistakes

- **Name mismatch**: `AddRedis("redis")` in AppHost but `AddRedisClient("cache")` in the service → client fails to resolve the connection string at startup
- **Missing `WaitFor`**: service starts before Redis is healthy → transient connection failures during boot
- **`AddRedisClient` vs `AddRedisDistributedCache`**: use `AddRedisDistributedCache` when pairing with HybridCache; use `AddRedisClient` when you need `IConnectionMultiplexer` directly
- **Adding `Aspire.StackExchange.Redis` to AppHost**: the hosting package (`Aspire.Hosting.Redis`) goes in AppHost; the client packages (`Aspire.StackExchange.Redis.*`) go in the consuming service
