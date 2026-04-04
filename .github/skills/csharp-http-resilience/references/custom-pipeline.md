# Custom Resilience Pipelines

Use `AddResilienceHandler` when you need full control over which strategies run and in what order. You get the same Polly building blocks as the standard handler, but assembled manually.

## Basic shape

```csharp
services.AddHttpClient<PaymentsClient>(c => c.BaseAddress = new("https://pay.example.com"))
    .AddResilienceHandler("PaymentsPipeline", pipeline =>
    {
        // Order matters: outermost strategy listed first
        pipeline.AddRetry(new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = 2,
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            Delay = TimeSpan.FromMilliseconds(500),
            // Safe for idempotent GETs, but be careful with mutations
            ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                .HandleResult(r => (int)r.StatusCode >= 500 || r.StatusCode == HttpStatusCode.RequestTimeout)
                .Handle<HttpRequestException>()
        });

        pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
        {
            SamplingDuration = TimeSpan.FromSeconds(30),
            FailureRatio = 0.5,
            MinimumThroughput = 5,
            BreakDuration = TimeSpan.FromSeconds(30),
            ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                .HandleResult(r => (int)r.StatusCode >= 500)
                .Handle<HttpRequestException>()
        });

        pipeline.AddTimeout(TimeSpan.FromSeconds(15));
    });
```

## HttpRetryStrategyOptions key properties

| Property | Description | Default |
|----------|-------------|---------|
| `MaxRetryAttempts` | Number of retries (not attempts) | 3 |
| `BackoffType` | `Constant`, `Linear`, `Exponential` | `Exponential` |
| `Delay` | Base delay between attempts | 2 s |
| `UseJitter` | Add randomness to spread retries | `true` |
| `ShouldHandle` | What to retry on | 5xx, 408, 429, `HttpRequestException`, `TimeoutRejectedException` |

## HttpCircuitBreakerStrategyOptions key properties

| Property | Description | Default |
|----------|-------------|---------|
| `FailureRatio` | Fraction of failures to trip breaker | 0.1 (10 %) |
| `MinimumThroughput` | Min requests before ratio applies | 100 |
| `SamplingDuration` | Sliding window | 30 s |
| `BreakDuration` | How long breaker stays open | 5 s |

## TimeoutRejectedException gotcha

When a retry wraps a timeout, Polly raises `TimeoutRejectedException` (not `TimeoutException`) when the attempt times out. If your `ShouldHandle` doesn't include it, the retry won't fire:

```csharp
// Wrong — TimeoutException is NOT what Polly throws
.Handle<TimeoutException>()

// Correct
.Handle<TimeoutRejectedException>()
// or just don't specify Handle<> for it — HttpRetryStrategyOptions handles it by default
```

Using `HttpRetryStrategyOptions` (the HTTP-specific type) rather than `RetryStrategyOptions<HttpResponseMessage>` makes this easier — it comes with sensible defaults already including `TimeoutRejectedException`.

## Dynamic reload from configuration

Use the two-argument overload to reload options at runtime without restarting:

```csharp
services.AddHttpClient<SearchClient>()
    .AddResilienceHandler(
        "SearchPipeline",
        (pipeline, context) =>
        {
            // Reloads whenever IOptionsMonitor<HttpRetryStrategyOptions>("SearchRetry") changes
            context.EnableReloads<HttpRetryStrategyOptions>("SearchRetry");

            var retryOptions = context.GetOptions<HttpRetryStrategyOptions>("SearchRetry");
            pipeline.AddRetry(retryOptions);
        });
```

Configure the named options in `appsettings.json` under the matching key and bind with `services.Configure<HttpRetryStrategyOptions>("SearchRetry", config.GetSection("SearchRetry"))`.

## Per-authority circuit breaker

If a named client talks to multiple host names (via routing or redirects), isolate circuit breaker state per authority so one unhealthy host doesn't trip the breaker for others:

```csharp
services.AddHttpClient("multi-region")
    .AddResilienceHandler("MultiRegionPipeline", pipeline =>
    {
        pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions());
    })
    .SelectPipelineByAuthority();
```
