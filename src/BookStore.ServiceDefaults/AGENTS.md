# Service Defaults Instructions

**Scope**: `src/BookStore.ServiceDefaults/**`

## Core Rules
- **Consistency**: Apply standard health checks, metrics, logging, and resilience across all services.
- **Extensions**: Keep `Extensions.cs` clean and focused on cross-cutting concerns.
- **Aspire Integration**: ServiceDefaults are automatically applied by Aspire orchestration.
- **Configuration**: Use Options pattern for configurable defaults.

## Purpose

ServiceDefaults provides shared infrastructure configuration that applies to both `ApiService` and `Web` projects. This ensures consistency in:
- OpenTelemetry (distributed tracing, metrics, logging)
- Health checks
- Resilience patterns (retry, circuit breaker)
- Service discovery

## Health Check Patterns

### Registering Health Checks
```csharp
public static IHostApplicationBuilder AddServiceDefaults(this IHostApplicationBuilder builder)
{
    builder.Services.AddHealthChecks()
        .AddCheck("self", () => HealthCheckResult.Healthy())
        .AddNpgSql(builder.Configuration.GetConnectionString("bookstoredb")!)
        .AddRedis(builder.Configuration.GetConnectionString("cache")!);

    return builder;
}
```

### Custom Health Checks
```csharp
public class CustomHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var isHealthy = /* check logic */;
        return Task.FromResult(
            isHealthy
                ? HealthCheckResult.Healthy("Service is healthy")
                : HealthCheckResult.Unhealthy("Service is unhealthy")
        );
    }
}

// Register
builder.Services.AddHealthChecks()
    .AddCheck<CustomHealthCheck>("custom");
```

## OpenTelemetry Configuration

### Tracing
```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.AddAspNetCoreInstrumentation()
               .AddHttpClientInstrumentation()
               .AddNpgsql()
               .AddSource("Marten")
               .AddSource("Wolverine");
    });
```

### Metrics
```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddAspNetCoreInstrumentation()
               .AddHttpClientInstrumentation()
               .AddRuntimeInstrumentation()
               .AddProcessInstrumentation();
    });
```

### Logging
```csharp
builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
});
```

## Resilience Patterns (Polly)

### Standard Resilience Handler
```csharp
public static IHttpClientBuilder AddStandardResilienceHandler(
    this IHttpClientBuilder builder)
{
    return builder.AddStandardResilienceHandler(options =>
    {
        options.Retry.MaxRetryAttempts = 3;
        options.Retry.Delay = TimeSpan.FromSeconds(1);
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(10);
        options.CircuitBreaker.FailureRatio = 0.5;
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(30);
    });
}
```

### Using Resilience in HttpClients
```csharp
// In ApiService or Web
builder.Services.AddHttpClient<IBooksClient>()
    .AddStandardResilienceHandler();  // From ServiceDefaults
```

## Service Discovery

### Configuring Service Discovery
```csharp
public static IHostApplicationBuilder AddServiceDiscovery(
    this IHostApplicationBuilder builder)
{
    builder.Services.AddServiceDiscovery();
    
    builder.Services.ConfigureHttpClientDefaults(http =>
    {
        http.AddServiceDiscovery();
    });

    return builder;
}
```

### Usage
```csharp
// In Web project, reference API service by name
builder.Services.AddHttpClient<IBooksClient>(client =>
{
    client.BaseAddress = new Uri("http://apiservice");  // Aspire resolves this
});
```

## Logging Enrichment

### Adding Correlation IDs
```csharp
builder.Services.AddHttpContextAccessor();
builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeScopes = true;  // Includes correlation/causation IDs
});
```

### Custom Log Enrichment
```csharp
public class CorrelationIdEnricher : ILogEventEnricher
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var correlationId = _httpContextAccessor.HttpContext?.Request.Headers["X-Correlation-ID"];
        if (!string.IsNullOrEmpty(correlationId))
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("CorrelationId", correlationId));
        }
    }
}
```

## When to Add New Service Defaults

Add to ServiceDefaults when:
- ✅ The configuration applies to **both** ApiService and Web
- ✅ It's a cross-cutting concern (logging, health, resilience)
- ✅ It requires consistent configuration across services
- ❌ Don't add business logic or domain-specific configuration

## Common Extensions

### Database Health Checks
- `AddNpgSql()` - PostgreSQL
- `AddSqlServer()` - SQL Server
- `AddRedis()` - Redis

### Telemetry Sources
- `AddAspNetCoreInstrumentation()` - HTTP requests
- `AddHttpClientInstrumentation()` - HTTP client calls
- `AddNpgsql()` - PostgreSQL queries
- `AddSource("Marten")` - Marten events
- `AddSource("Wolverine")` - Wolverine messages

### Resilience Handlers
- `AddStandardResilienceHandler()` - Retry + Circuit Breaker + Timeout
- `AddRetryPolicy()` - Retry only
- `AddCircuitBreakerPolicy()` - Circuit breaker only

## Troubleshooting

### Health Check Failing
- Check connection strings in `appsettings.json`
- Ensure Docker containers are running (PostgreSQL, Redis)
- View health check details at `/health` endpoint

### Telemetry Not Appearing
- Ensure Aspire dashboard is running
- Check OpenTelemetry exporters are configured
- Verify instrumentation sources are added

### Service Discovery Not Working
- Ensure service names match between AppHost and HttpClient
- Check Aspire dashboard for service registration
- Verify `AddServiceDiscovery()` is called in ServiceDefaults

## References
- See [Configuration Guide](../../docs/configuration-guide.md) for Options pattern
- See [Correlation & Causation IDs](../../docs/correlation-causation-guide.md) for distributed tracing
- See [Logging Guide](../../docs/logging-guide.md) for structured logging patterns

