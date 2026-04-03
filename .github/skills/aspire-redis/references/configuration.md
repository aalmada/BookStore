# Aspire Redis — Configuration, Health Checks & Telemetry

## Configuration sources

Redis client settings are resolved in this priority order:

### 1. Aspire `WithReference` (preferred in Aspire apps)

When `WithReference(cache)` is used in AppHost, Aspire injects a `ConnectionStrings__cache` environment variable automatically. No additional configuration is needed.

### 2. `ConnectionStrings` section

```json
// appsettings.json
{
  "ConnectionStrings": {
    "cache": "localhost:6379"
  }
}
```

For format details, see the [StackExchange.Redis configuration docs](https://stackexchange.github.io/StackExchange.Redis/Configuration.html#basic-configuration-strings).

### 3. `Aspire:StackExchange:Redis` configuration section

```json
{
  "Aspire": {
    "StackExchange": {
      "Redis": {
        "ConnectionString": "localhost:6379",
        "DisableHealthChecks": false,
        "DisableTracing": false
      }
    }
  }
}
```

### 4. Inline delegate

```csharp
builder.AddRedisClient(
    ResourceNames.Cache,
    static settings => settings.DisableTracing = true);
```

## Available `StackExchangeRedisSettings` options

| Setting | Default | Description |
|---------|---------|-------------|
| `ConnectionString` | from Aspire | Override the connection string |
| `DisableHealthChecks` | `false` | Disable Redis health check registration |
| `DisableTracing` | `false` | Disable OpenTelemetry tracing |

## Health checks

Enabled by default. The Redis integration registers a health check that:
- Verifies the Redis instance is reachable
- Executes a `PING` command to confirm command processing

To disable:
```csharp
builder.AddRedisClient(
    ResourceNames.Cache,
    static settings => settings.DisableHealthChecks = true);
```

## Observability

### Logging
Log category: `Aspire.StackExchange.Redis`

### Tracing
Activity source: `OpenTelemetry.Instrumentation.StackExchangeRedis`

Traces Redis commands through distributed traces, letting you see Redis latency alongside your HTTP requests in the Aspire dashboard.

### Metrics
Metrics are emitted via OpenTelemetry. The Aspire dashboard displays Redis metrics when telemetry is configured (done automatically by `AddServiceDefaults()`).

## Connection properties exposed via Aspire

When a service references a Redis resource, Aspire injects these environment variables (for a resource named `cache`):

| Property | Environment Variable | Description |
|----------|---------------------|-------------|
| Host | `CACHE_HOST` | Hostname / IP |
| Port | `CACHE_PORT` | Port (default 6379) |
| Password | `CACHE_PASSWORD` | Auth password |
| Uri | `CACHE_URI` | Full URI: `redis://:{Password}@{Host}:{Port}` |
| Connection string | `ConnectionStrings__cache` | Full StackExchange.Redis connection string |
