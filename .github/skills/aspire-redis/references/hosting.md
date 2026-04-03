# Aspire Redis — Hosting Integration (AppHost)

## Package

Add `Aspire.Hosting.Redis` to your AppHost project:

```xml
<!-- BookStore.AppHost.csproj -->
<PackageReference Include="Aspire.Hosting.Redis" />
```

Or via the CLI:
```bash
aspire add redis
```

## Add a Redis resource

```csharp
// AppHost.cs
var cache = builder.AddRedis(ResourceNames.Cache);

builder.AddProject<Projects.MyService>(ResourceNames.ApiService)
    .WithReference(cache)   // injects ConnectionStrings__cache
    .WaitFor(cache);        // wait for Redis to be ready before starting
```

`WaitFor` prevents transient boot failures when the service starts before Redis is healthy. Always include it.

## Connect to an existing Redis instance

```csharp
// Use an existing Redis (local or remote) instead of spinning up a container
var cache = builder.AddRedis(ResourceNames.Cache)
    .AsExisting();
```

When using `AsExisting`, the connection string must be in the AppHost's configuration:

```json
// appsettings.json (or user secrets)
{
  "ConnectionStrings": {
    "cache": "localhost:6379"
  }
}
```

## Data persistence

By default, the Redis container is ephemeral. To persist data across restarts:

**Data volume** (recommended for local dev):
```csharp
var cache = builder.AddRedis(ResourceNames.Cache)
    .WithDataVolume();      // mounts /data volume
```

**With snapshot persistence**:
```csharp
var cache = builder.AddRedis(ResourceNames.Cache)
    .WithDataVolume()
    .WithPersistence(
        interval: TimeSpan.FromMinutes(5),   // save every 5 min
        keysChangedThreshold: 100);           // or when 100 keys change
```

**Bind mount** (maps a host path into the container):
```csharp
var cache = builder.AddRedis(ResourceNames.Cache)
    .WithDataBindMount(source: "/path/on/host");
```

## Management UIs

Add a web-based UI to inspect Redis data alongside the Aspire dashboard:

```csharp
// Redis Insight — free official Redis GUI
var cache = builder.AddRedis(ResourceNames.Cache)
    .WithRedisInsight();

// Redis Commander — Node.js web UI
var cache = builder.AddRedis(ResourceNames.Cache)
    .WithRedisCommander();

// DbGate (Community Toolkit) — multi-database GUI
// Requires: CommunityToolkit.Aspire.Hosting.Redis.Extensions
var cache = builder.AddRedis(ResourceNames.Cache)
    .WithDbGate();
```

## Persistent container lifetime

To avoid the Redis container stopping when the AppHost stops (useful for outer loop development):

```csharp
var cache = builder.AddRedis(ResourceNames.Cache)
    .WithLifetime(ContainerLifetime.Persistent);
```

## Pass connection info to non-.NET services

Use `WithReference` for .NET projects; for other runtimes, inject individual properties:

```csharp
builder.AddExecutable("worker", "python", "worker.py", ".")
    .WithReference(cache)  // ConnectionStrings__cache
    .WithEnvironment(ctx =>
    {
        ctx.EnvironmentVariables["REDIS_HOST"] =
            cache.Resource.PrimaryEndpoint.Property(EndpointProperty.Host);
        ctx.EnvironmentVariables["REDIS_PORT"] =
            cache.Resource.PrimaryEndpoint.Property(EndpointProperty.Port);
    });
```

## Health checks

The hosting integration registers a health check automatically. The Redis resource appears in the Aspire dashboard resource list and is considered healthy when a TCP connection succeeds.
