# DI Registration

## Basic registration with `AddRefitClient<T>`

Requires `Refit.HttpClientFactory`. Returns an `IHttpClientBuilder` so you can chain `ConfigureHttpClient`, `AddHttpMessageHandler`, and resilience handlers.

```csharp
// Single client
services.AddRefitClient<IOrdersClient>()
    .ConfigureHttpClient(c => c.BaseAddress = new Uri("https://orders.example.com"));
```

## Adding DelegatingHandlers

Register handlers as **transient** (not singleton), then add them via `AddHttpMessageHandler<T>`. Handlers run in the order they are added — first added = outermost.

```csharp
// Register handlers
services.AddTransient<AuthHandler>();
services.AddTransient<CorrelationIdHandler>();

// Build client with handler chain: Auth → CorrelationId → Network
services.AddRefitClient<IOrdersClient>()
    .ConfigureHttpClient(c => c.BaseAddress = new Uri("https://orders.example.com"))
    .AddHttpMessageHandler<AuthHandler>()
    .AddHttpMessageHandler<CorrelationIdHandler>();
```

**Handler chain execution direction:**
- Send: AuthHandler → CorrelationIdHandler → Network (adds headers outbound)
- Receive: Network → CorrelationIdHandler → AuthHandler (processes response inbound)

This means put resilience outermost (before auth) so retries re-execute auth. See [authentication.md](authentication.md) for the DelegatingHandler pattern.

## RefitSettings — serializer and behaviour

Pass settings to `AddRefitClient<T>(settings)` or via a factory to access DI:

```csharp
// Static settings
var settings = new RefitSettings
{
    ContentSerializer = new SystemTextJsonContentSerializer(
        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower })
};
services.AddRefitClient<IOrdersClient>(settings)
    .ConfigureHttpClient(c => c.BaseAddress = new Uri("https://orders.example.com"));

// Settings from DI container
services.AddRefitClient<IOrdersClient>(sp =>
    new RefitSettings
    {
        ContentSerializer = sp.GetRequiredService<IHttpContentSerializer>()
    })
    .ConfigureHttpClient(c => c.BaseAddress = new Uri("https://orders.example.com"));
```

Key `RefitSettings` properties:

| Property | Purpose |
|----------|---------|
| `ContentSerializer` | Swap `SystemTextJsonContentSerializer` (default) for Newtonsoft or custom |
| `ExceptionFactory` | Suppress or replace the exception thrown on non-2xx when using `Task<T>` |
| `DeserializationExceptionFactory` | Handle failures when deserializing the response body |
| `AuthorizationHeaderValueGetter` | Async bearer token supplier (alternative to DelegatingHandler) |
| `CollectionFormat` | How `IEnumerable<T>` query params are encoded (default: `Multi`) |

## Integration with resilience

Chain `AddStandardResilienceHandler()` or `AddResilienceHandler(...)` after the Refit registration. See the `csharp-http-resilience` skill for full details.

```csharp
services.AddRefitClient<IOrdersClient>()
    .ConfigureHttpClient(c => c.BaseAddress = new Uri("https://orders.example.com"))
    .AddHttpMessageHandler<AuthHandler>()
    .AddStandardResilienceHandler(opts =>
    {
        // POST/PUT/DELETE mutations are not safe to auto-retry
        opts.Retry.DisableForUnsafeHttpMethods();
    });
```

## Registering multiple related clients

Extract a helper to avoid repetition:

```csharp
public static IServiceCollection AddMyApiClients(this IServiceCollection services, Uri baseAddress)
{
    services.AddTransient<AuthHandler>();

    services
        .AddRefitClient<IOrdersClient>()
        .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
        .AddHttpMessageHandler<AuthHandler>()
        .AddStandardResilienceHandler();

    services
        .AddRefitClient<IProductsClient>()
        .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
        .AddHttpMessageHandler<AuthHandler>()
        .AddStandardResilienceHandler();

    return services;
}
```

## Scoped clients (Blazor Server / per-request context)

When the client needs scoped services (e.g., a per-circuit access token, per-request tenant ID), construct it with `RestService.For<T>` inside a scoped factory. **Do not** register as singleton when it captures scoped state.

```csharp
services.AddScoped<IOrdersClient>(sp =>
{
    var tokenService = sp.GetRequiredService<ITokenService>();
    var tenantService = sp.GetRequiredService<ITenantService>();

    // Do NOT set InnerHandler to null here — wire the chain manually
    var networkHandler = new HttpClientHandler();
    var tenantHandler = new TenantHeaderHandler(tenantService) { InnerHandler = networkHandler };
    var authHandler = new AuthHandler(tokenService) { InnerHandler = tenantHandler };

    var httpClient = new HttpClient(authHandler)
    {
        BaseAddress = new Uri("https://orders.example.com")
    };
    return RestService.For<IOrdersClient>(httpClient);
});
```

> **Caution**: When using `RestService.For<T>` you own the `HttpClient` lifetime. Dispose it appropriately or register as scoped.

## Common mistakes

- **Registering handlers as singleton**: if the handler captures scoped services it will use stale state after the first request. Always `AddTransient`.
- **Forgetting `ConfigureHttpClient`**: without a `BaseAddress` Refit builds a client with no base URL and all requests fail immediately.
- **Registering a scoped client as singleton**: a `RestService.For<T>` factory that captures scoped services (auth token, tenant) must be `AddScoped`.
- **Using `AddPolicyHandler` (Polly v7 / `Microsoft.Extensions.Http.Polly`)**: this package is deprecated — use `AddStandardResilienceHandler` from `Microsoft.Extensions.Http.Resilience` instead.
