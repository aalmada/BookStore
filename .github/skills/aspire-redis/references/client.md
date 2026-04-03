# Aspire Redis — Client Integration

## Choosing the right client package

| Scenario | Package | Registration method |
|----------|---------|---------------------|
| HybridCache L2 / `IDistributedCache` | `Aspire.StackExchange.Redis.DistributedCaching` | `AddRedisDistributedCache` |
| Raw `IConnectionMultiplexer` access | `Aspire.StackExchange.Redis` | `AddRedisClient` |
| Fluent builder with caching config | `Aspire.StackExchange.Redis` | `AddRedisClientBuilder` |
| Azure Cache for Redis (Entra auth) | `Aspire.Microsoft.Azure.StackExchangeRedis` | `AddRedisClientBuilder` + `.WithAzureAuthentication()` |

Add only what you need — you don't need both DistributedCaching and the raw client packages unless you're using both APIs.

## Distributed cache (`IDistributedCache`)

Best for use with `HybridCache` as the L2 tier.

```xml
<!-- MyService.csproj -->
<PackageReference Include="Aspire.StackExchange.Redis.DistributedCaching" />
```

```csharp
// Program.cs
builder.AddRedisDistributedCache(ResourceNames.Cache);

// HybridCache automatically picks up the registered IDistributedCache as L2
builder.Services.AddHybridCache();
```

The `connectionName` must match the name used in AppHost's `AddRedis(...)`.

## Raw Redis client (`IConnectionMultiplexer`)

For direct Redis commands (pub/sub, Lua scripts, custom data structures).

```xml
<PackageReference Include="Aspire.StackExchange.Redis" />
```

```csharp
builder.AddRedisClient(ResourceNames.Cache);

// Inject via DI
public class MyService(IConnectionMultiplexer redis)
{
    public async Task PublishAsync(string channel, string message)
    {
        var pub = redis.GetSubscriber();
        await pub.PublishAsync(RedisChannel.Literal(channel), message);
    }
}
```

## Multiple Redis instances (keyed)

When your service connects to more than one Redis resource:

```csharp
// AppHost.cs
var cache = builder.AddRedis("cache");
var queue = builder.AddRedis("queue");

builder.AddProject<Projects.MyService>()
    .WithReference(cache)
    .WithReference(queue);
```

```csharp
// Program.cs
builder.AddKeyedRedisClient("cache");
builder.AddKeyedRedisClient("queue");

// Inject with [FromKeyedServices]
public class MyService(
    [FromKeyedServices("cache")] IConnectionMultiplexer cacheRedis,
    [FromKeyedServices("queue")] IConnectionMultiplexer queueRedis)
{ }
```

## Fluent builder pattern

Combine client setup and cache configuration in a single chain:

```csharp
builder.AddRedisClientBuilder(ResourceNames.Cache)
    .WithDistributedCache(options =>
    {
        options.InstanceName = "MyApp";
    });
```

For Azure Cache for Redis with Entra ID authentication:

```xml
<PackageReference Include="Aspire.Microsoft.Azure.StackExchangeRedis" />
```

```csharp
builder.AddRedisClientBuilder(ResourceNames.Cache)
    .WithAzureAuthentication()
    .WithDistributedCache(options =>
    {
        options.InstanceName = "MyApp";
    });
```

## Auto-activation

Redis clients use lazy initialization by default. Enable auto-activation to detect connection failures at startup rather than on first use:

```csharp
builder.AddRedisClient(ResourceNames.Cache, c => c.DisableAutoActivation = false);
```

This is planned to become the default in a future Aspire release.
