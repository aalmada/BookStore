# Refit Integration

Refit clients can be wired with resilience in two ways depending on whether they are managed by `IHttpClientFactory` (cleaner, recommended) or constructed manually (needed when clients are scoped).

## IHttpClientFactory-based (recommended)

Use `AddRefitClient<T>()` from `Refit.HttpClientFactory`, then chain `AddResilienceHandler` or `AddStandardResilienceHandler` exactly like any other typed client:

```csharp
// Simple — standard resilience, no custom options
services
    .AddRefitClient<IOrdersClient>()
    .ConfigureHttpClient(c => c.BaseAddress = new("https://orders.example.com"))
    .AddStandardResilienceHandler(opts =>
    {
        opts.Retry.DisableForUnsafeHttpMethods(); // POST/PUT/DELETE not idempotent
    });

// Custom pipeline
services
    .AddRefitClient<ISearchClient>()
    .ConfigureHttpClient(c => c.BaseAddress = new("https://search.example.com"))
    .AddResilienceHandler("SearchPipeline", pipeline =>
    {
        pipeline.AddRetry(new HttpRetryStrategyOptions { MaxRetryAttempts = 5 });
        pipeline.AddTimeout(TimeSpan.FromSeconds(8));
    });
```

## Manually-constructed Refit (Blazor scoped pattern)

In Blazor Server you often need scoped clients that capture per-circuit services (auth tokens, tenant context). Use the static-client pattern from [static-clients.md](static-clients.md) and pass the final `HttpClient` to `RestService.For<T>`:

```csharp
services.AddScoped<IOrdersClient>(sp =>
{
    var pipeline = sp.GetRequiredService<ResiliencePipeline<HttpResponseMessage>>();
    var tokenService = sp.GetRequiredService<TokenService>();
    var tenantService = sp.GetRequiredService<TenantService>();

    // Handler chain: Resilience → Auth → Tenant → Network
    var networkHandler = new HttpClientHandler();
    var tenantHandler = new TenantHeaderHandler(tenantService) { InnerHandler = networkHandler };
    var authHandler = new AuthHandler(tokenService) { InnerHandler = tenantHandler };
    var resilienceHandler = new ResilienceHandler(pipeline) { InnerHandler = authHandler };

    var httpClient = new HttpClient(resilienceHandler) { BaseAddress = new("https://api.example.com") };
    return RestService.For<IOrdersClient>(httpClient);
});
```

The shared `ResiliencePipeline<HttpResponseMessage>` is registered as a singleton (built once at startup) and injected. See [static-clients.md](static-clients.md) for how to build and register it.

## Choosing between the two

| Scenario | Approach |
|----------|----------|
| Standalone API / worker / console app | `AddRefitClient<T>().AddStandardResilienceHandler()` |
| Blazor Server with per-circuit auth/tenant | Manual `RestService.For<T>(httpClient)` with `ResilienceHandler` |
| Multiple clients sharing same pipeline | Build shared `ResiliencePipeline<HttpResponseMessage>` singleton |
| Clients needing different resilience options | Separate `AddResilienceHandler` calls per client |

## Common mistake: registering scoped Refit clients as singletons

If you register a Refit client as `AddSingleton` but it captures a scoped `TokenService`, you'll get a captive dependency bug — the first request claims a token and every subsequent request uses the same stale token. Always register as `AddScoped` when the client captures scoped services.
