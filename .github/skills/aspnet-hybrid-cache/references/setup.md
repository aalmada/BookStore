# HybridCache Setup

## Registration

### Aspire: wire Redis as the L2 provider

In `Program.cs`, register the Aspire Redis resource before building the app:

```csharp
// src/BookStore.ApiService/Program.cs
builder.AddRedisDistributedCache(ResourceNames.Cache);
```

`ResourceNames.Cache` is the name of the Redis resource as declared in the AppHost.

### DI service registration

In your services extension method, call `AddHybridCache()` **after** `AddRedisDistributedCache()`:

```csharp
// src/BookStore.ApiService/Infrastructure/Extensions/ApplicationServicesExtensions.cs
services.AddHybridCache(options =>
{
    // Optional global defaults — overridden per-call by HybridCacheEntryOptions
    options.DefaultEntryOptions = new HybridCacheEntryOptions
    {
        Expiration = TimeSpan.FromMinutes(5),
        LocalCacheExpiration = TimeSpan.FromMinutes(2)
    };
});
```

`AddHybridCache()` is an extension method on `IServiceCollection` from the `Microsoft.Extensions.Caching.Hybrid` package (included in .NET 9+ SDK; add the NuGet package for .NET 8).

### AppHost: declare the Redis resource

```csharp
// src/BookStore.AppHost/Program.cs
var cache = builder.AddRedis("cache");

builder.AddProject<Projects.BookStore_ApiService>("apiservice")
    .WithReference(cache);
```

The Aspire resource name `"cache"` must match `ResourceNames.Cache`.

## Injecting HybridCache

`HybridCache` is abstract. Inject it by its abstract type — ASP.NET Core registers a concrete implementation:

```csharp
// Minimal API endpoint parameter binding
app.MapGet("/books", async ([FromServices] HybridCache cache, ...) => { ... });

// Constructor injection in handlers/services
public sealed class MyHandler(HybridCache cache)
{
    ...
}
```

Do **not** inject `IMemoryCache` or `IDistributedCache` directly in new code; the `HybridCache` abstraction manages both.

## Required NuGet packages

| Package | Purpose |
|---------|---------|
| `Microsoft.Extensions.Caching.Hybrid` | `HybridCache` abstract class + `HybridCacheEntryOptions` |
| `Microsoft.Extensions.Caching.StackExchangeRedis` | `IDistributedCache` on Redis (pulled in automatically by Aspire) |

Both are transitively included when using Aspire's `AddRedisDistributedCache`. No additional package reference is needed in most projects.
