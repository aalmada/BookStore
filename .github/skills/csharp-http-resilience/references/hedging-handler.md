# Hedging Handler

Hedging is a different resilience pattern from retry: instead of waiting for a failure before retrying, it proactively issues a second (or third) request in parallel when the first one is slow. Use hedging when latency matters more than request volume.

## When to prefer hedging over retry

- **Retry**: best when the dependency is occasionally down or rate-limiting. Tolerates higher latency.
- **Hedging**: best when the dependency is usually available but sometimes slow (tail latency). Doubles request volume but halves p99.

## Basic usage

```csharp
services.AddHttpClient<SearchClient>(c => c.BaseAddress = new("https://search.example.com"))
    .AddStandardHedgingHandler();
```

## Default hedging pipeline (outermost → innermost, per attempt)

| Order | Strategy | Default |
|-------|----------|---------|
| 1 | Total request timeout | 30 s |
| 2 | Hedging | Min 1 attempt, max 10 concurrent, 2 s delay before hedging |
| 3 | Rate limiter (per endpoint) | Queue: 0, Permit: 1 000 |
| 4 | Circuit breaker (per endpoint) | 10 % failure ratio, 100 req minimum, 30 s window, 5 s break |
| 5 | Attempt timeout (per endpoint) | 10 s |

The per-endpoint circuit breaker ensures one slow endpoint doesn't cascade to healthy ones.

## SelectPipelineByAuthority (important)

The circuit breaker pool is keyed by URL authority by default. Make this explicit to avoid accidentally sharing state across different base addresses:

```csharp
services.AddHttpClient("search")
    .AddStandardHedgingHandler()
    .SelectPipelineByAuthority();
```

## Weighted routing (A/B testing)

```csharp
services.AddHttpClient<SearchClient>()
    .AddStandardHedgingHandler(builder =>
    {
        builder.ConfigureWeightedGroups(opts =>
        {
            opts.SelectionMode = WeightedGroupSelectionMode.EveryAttempt;
            opts.Groups.Add(new WeightedUriEndpointGroup
            {
                Endpoints =
                {
                    new() { Uri = new("https://search-v2.example.com"), Weight = 10 },
                    new() { Uri = new("https://search.example.com"), Weight = 90 }
                }
            });
        });
    });
```

## Ordered failover

```csharp
services.AddHttpClient<SearchClient>()
    .AddStandardHedgingHandler(builder =>
    {
        builder.ConfigureOrderedGroups(opts =>
        {
            opts.Groups.Add(new UriEndpointGroup
            {
                Endpoints =
                {
                    new() { Uri = new("https://primary.example.com"), Weight = 97 },
                    new() { Uri = new("https://fallback.example.com"), Weight = 3 }
                }
            });
        });
    });
```

The maximum number of hedging attempts equals the number of configured endpoint groups.
