---
name: csharp-http-resilience
description: Add retry, circuit breaker, timeout, hedging, and rate-limiting to HttpClient-based .NET code using Microsoft.Extensions.Http.Resilience (built on Polly v8). Covers AddStandardResilienceHandler, AddResilienceHandler, AddStandardHedgingHandler, ResiliencePipelineBuilder for static clients, Refit integration, and the ResilienceHandler wrapper. Trigger whenever the user writes, reviews, or asks about HTTP resilience, retries, circuit breakers, timeouts, transient fault handling, AddStandardResilienceHandler, AddResilienceHandler, AddStandardHedgingHandler, Polly, IHttpClientFactory resilience, or HttpClient reliability in .NET — even if they don't mention Microsoft.Extensions.Http.Resilience by name. Always prefer this skill over guessing; the handler-stacking rules, retry safety for non-idempotent methods, TimeoutRejectedException vs TimeoutException distinction, and static-client wiring all have non-obvious failure modes. Also trigger when the deprecated Microsoft.Extensions.Http.Polly package is used — it should be replaced with this package.
---

# Microsoft.Extensions.Http.Resilience Skill

`Microsoft.Extensions.Http.Resilience` is the official .NET integration layer between `IHttpClientFactory` and Polly v8 resilience pipelines. It replaces the deprecated `Microsoft.Extensions.Http.Polly` package and provides first-class DI support for retry, circuit breaker, timeout, rate limiting, and hedging.

**NuGet**: `<PackageReference Include="Microsoft.Extensions.Http.Resilience" />`

## Quick reference

| Topic | See |
|-------|-----|
| `AddStandardResilienceHandler`, defaults pipeline, DisableFor, global defaults | [standard-handler.md](references/standard-handler.md) |
| `AddStandardHedgingHandler`, routing strategies, SelectPipelineByAuthority | [hedging-handler.md](references/hedging-handler.md) |
| `AddResilienceHandler` custom pipelines, HttpRetryStrategyOptions, dynamic reload | [custom-pipeline.md](references/custom-pipeline.md) |
| `ResiliencePipelineBuilder` + `ResilienceHandler` for static/singleton clients | [static-clients.md](references/static-clients.md) |
| Refit integration (DI-registered and manually-constructed) | [refit-integration.md](references/refit-integration.md) |

## Two approaches

### 1. IHttpClientFactory-based (recommended)

Wire resilience into the `IHttpClientBuilder` chain during DI registration. This is the idiomatic approach for typed or named clients.

```csharp
// Typed client — single method, all defaults
builder.Services
    .AddHttpClient<MyApiClient>(c => c.BaseAddress = new("https://api.example.com"))
    .AddStandardResilienceHandler();

// Named client — custom pipeline
services.AddHttpClient("payments")
    .AddResilienceHandler("PaymentsPipeline", pipeline =>
    {
        pipeline.AddRetry(new HttpRetryStrategyOptions { MaxRetryAttempts = 2 });
        pipeline.AddTimeout(TimeSpan.FromSeconds(10));
    });
```

### 2. Static / manually-constructed HttpClient

When you construct `HttpClient` directly (e.g., Refit with scoped DI, or a singleton not owned by DI), build the pipeline and wrap the handler chain manually. See [static-clients.md](references/static-clients.md).

## Critical rules

- **Never stack resilience handlers.** Only add _one_ resilience handler per client. If you need multiple strategies combine them inside a single `AddResilienceHandler` call.
- **Retry is unsafe for non-idempotent methods.** Always call `options.Retry.DisableForUnsafeHttpMethods()` (or `DisableFor(HttpMethod.Post, ...)`) when POST/PUT/DELETE mutate state.
- **Timeout exception type.** Polly throws `TimeoutRejectedException`, not `TimeoutException`. When writing `ShouldHandle` predicates in a retry that sits outside a timeout, handle `TimeoutRejectedException`.
- **Circuit breaker per authority.** When using a circuit breaker on named clients shared across multiple host names, call `.SelectPipelineByAuthority()` so each host gets its own breaker state.
- **`Microsoft.Extensions.Http.Polly` is deprecated.** Replace any `AddPolicyHandler` / `AddTransientHttpErrorPolicy` calls with the APIs in this skill.
