# Static / Manually-Constructed Clients

When you build `HttpClient` manually — bypassing `IHttpClientFactory` — you can still get full resilience by using `ResiliencePipelineBuilder<HttpResponseMessage>` and wrapping your handler chain with `ResilienceHandler`.

This pattern is necessary when:
- Clients are registered as _scoped_ singletons (e.g., Refit clients in Blazor circuits)
- You must thread custom `DelegatingHandler`s into a specific position in the chain
- You can't use DI-managed `IHttpClientFactory` lifetime for technical reasons

## Build the pipeline once (shared)

Build the pipeline once (e.g., at startup) and share it across multiple client instances. The pipeline itself is thread-safe.

```csharp
// Built once at startup, shared across scoped clients
var resiliencePipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
    .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
    {
        MaxRetryAttempts = 3,
        BackoffType = DelayBackoffType.Exponential,
        UseJitter = true,
        Delay = TimeSpan.FromMilliseconds(500),
        ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
            .HandleResult(r => (int)r.StatusCode >= 500 || r.StatusCode == HttpStatusCode.RequestTimeout)
            .Handle<HttpRequestException>()
            .Handle<TaskCanceledException>()
    })
    .AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
    {
        FailureRatio = 0.5,
        SamplingDuration = TimeSpan.FromSeconds(30),
        MinimumThroughput = 5,
        BreakDuration = TimeSpan.FromSeconds(30),
        ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
            .HandleResult(r => (int)r.StatusCode >= 500)
            .Handle<HttpRequestException>()
    })
    .AddTimeout(TimeSpan.FromSeconds(30))
    .Build();
```

Note: use Polly's generic `RetryStrategyOptions<HttpResponseMessage>` and `CircuitBreakerStrategyOptions<HttpResponseMessage>` here (not the HTTP-specific variants), since you're building outside `IHttpClientFactory`.

## Wrap the handler chain

Place `ResilienceHandler` at the _outermost_ position so it sees all responses before inner handlers:

```csharp
// Handler chain (outermost → innermost):
// ResilienceHandler → AuthHandler → TenantHandler → HttpClientHandler (network)
var networkHandler = new HttpClientHandler();
var tenantHandler = new TenantHeaderHandler(tenantService) { InnerHandler = networkHandler };
var authHandler = new AuthorizationHandler(tokenService) { InnerHandler = tenantHandler };
var resilienceHandler = new ResilienceHandler(resiliencePipeline) { InnerHandler = authHandler };

var httpClient = new HttpClient(resilienceHandler) { BaseAddress = baseAddress };
```

## Using namespaces

```csharp
using Microsoft.Extensions.Http.Resilience; // ResilienceHandler
using Polly;                                 // ResiliencePipelineBuilder<T>
using Polly.CircuitBreaker;                  // CircuitBreakerStrategyOptions<T>
using Polly.Retry;                           // RetryStrategyOptions<T>
```

## Scoped DI registration (e.g., Blazor)

Clients that depend on scoped services must themselves be scoped. Register the shared pipeline as a singleton and inject it:

```csharp
// Singleton pipeline (built once)
builder.Services.AddSingleton(resiliencePipeline);

// Scoped client factory
builder.Services.AddScoped<IMyApiClient>(sp =>
{
    var pipeline = sp.GetRequiredService<ResiliencePipeline<HttpResponseMessage>>();
    var authHandler = sp.GetRequiredService<AuthorizationHandler>();

    var networkHandler = new HttpClientHandler();
    authHandler.InnerHandler = networkHandler;
    var resilienceHandler = new ResilienceHandler(pipeline) { InnerHandler = authHandler };

    var httpClient = new HttpClient(resilienceHandler) { BaseAddress = baseAddress };
    return RestService.For<IMyApiClient>(httpClient);
});
```

## Trade-offs vs IHttpClientFactory approach

| Concern | IHttpClientFactory (`AddResilienceHandler`) | Manual (`ResilienceHandler`) |
|---------|--------------------------------------------|-----------------------------|
| Socket pooling | Handled automatically | On you (manage `HttpClientHandler` lifecycle) |
| DI lifetime | Framework-managed | Manual scoping required |
| Dynamic reload | `EnableReloads` built-in | Must rebuild pipeline manually |
| Handler chain control | Limited (Polly runs at a fixed position) | Full control — position anywhere |
| Refit typed clients | Use `AddRefitClient<T>().AddResilienceHandler(...)` | `RestService.For<T>(httpClient)` |

Prefer the IHttpClientFactory approach unless you have a specific reason to construct clients manually.
