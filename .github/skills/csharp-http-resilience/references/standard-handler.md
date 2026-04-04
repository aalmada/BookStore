# Standard Resilience Handler

`AddStandardResilienceHandler()` wires five Polly strategies in a sensible default order with out-of-the-box settings that cover most APIs. It is the right starting point unless you specifically need hedging or a hand-tuned pipeline.

## Default pipeline (outermost → innermost)

| Order | Strategy | What it does | Default |
|-------|----------|-------------|---------|
| 1 | Rate limiter | Caps concurrent requests | Queue: 0, Permit: 1 000 |
| 2 | Total timeout | Overall time limit including retries | 30 s |
| 3 | Retry | Retries on transient errors | 3 attempts, exponential backoff + jitter, 2 s base |
| 4 | Circuit breaker | Opens on sustained failures | 10 % failure ratio, min 100 requests, 30 s window, 5 s break |
| 5 | Attempt timeout | Per-attempt time limit | 10 s |

**Handled by retry and circuit breaker**: HTTP 5xx, 408, 429, `HttpRequestException`, `TimeoutRejectedException`.

## Basic usage

```csharp
// Typed client
services.AddHttpClient<WeatherClient>(c => c.BaseAddress = new("https://api.weather.com"))
    .AddStandardResilienceHandler();

// Named client
services.AddHttpClient("external-api")
    .AddStandardResilienceHandler();
```

## Customising options

Pass a delegate to override individual strategy options without replacing the whole pipeline:

```csharp
services.AddHttpClient<OrdersClient>(c => c.BaseAddress = new("https://orders.example.com"))
    .AddStandardResilienceHandler(options =>
    {
        // Tighten the total timeout for latency-sensitive callers
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(10);

        // Fewer retries for this client
        options.Retry.MaxRetryAttempts = 2;

        // Don't retry writes — risks duplicate records
        options.Retry.DisableForUnsafeHttpMethods();

        // Tighter circuit breaker
        options.CircuitBreaker.FailureRatio = 0.3;
        options.CircuitBreaker.MinimumThroughput = 20;
    });
```

### DisableFor vs DisableForUnsafeHttpMethods

- `DisableFor(HttpMethod.Post, HttpMethod.Delete)` — explicit list
- `DisableForUnsafeHttpMethods()` — disables retries for POST, PUT, PATCH, DELETE, CONNECT per RFC 7231 idempotency rules

Always use one of these when the API performs state mutations.

## Global defaults

Apply a resilience handler to all registered `HttpClient` instances in one go, then selectively override:

```csharp
// All clients get standard resilience
services.ConfigureHttpClientDefaults(b => b.AddStandardResilienceHandler());

// High-priority client: swap to hedging and remove the standard handler
services.AddHttpClient("priority")
    .RemoveAllResilienceHandlers()
    .AddStandardHedgingHandler();
```

`RemoveAllResilienceHandlers()` clears every previously registered resilience handler, giving you a clean slate. Use it after `ConfigureHttpClientDefaults` when a specific client needs different behaviour.

## Aspire integration

When using .NET Aspire service defaults (`AddServiceDefaults`), resilience is pre-configured via `ConfigureHttpClientDefaults`. Review [BookStore.ServiceDefaults/AGENTS.md](../../../../src/BookStore.ServiceDefaults/AGENTS.md) to understand what's already wired in before adding another handler.
