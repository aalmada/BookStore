# LoggerMessage Advanced Topics

## Logging Exceptions

`Exception` is always treated as the first positional parameter **after** `ILogger`. It is captured automatically by the runtime — do **not** include it in the message template.

```csharp
// ✅ Correct: Exception before other parameters, not in template
[LoggerMessage(
    EventId = 5001,
    Level = LogLevel.Error,
    Message = "Failed to connect to {Host}:{Port}")]
public static partial void ConnectionFailed(
    ILogger logger,
    Exception ex,           // ← before other params
    string host,
    int port);

// ❌ Wrong: Exception in template
[LoggerMessage(Level = LogLevel.Error, Message = "Error: {Ex}")]
public static partial void BadPattern(ILogger logger, Exception ex);
// → compiler emits SYSLIB0025 warning
```

The exception stack trace appears automatically in the output alongside the formatted message.

## Multiple exceptions

Only the *first* `Exception` parameter is treated specially. Extra `Exception` parameters are treated as regular template parameters and **must** appear in the message template:

```csharp
[LoggerMessage(
    EventId = 5002,
    Level = LogLevel.Error,
    Message = "Fallback error: {FallbackEx}")]
public static partial void FallbackFailed(
    ILogger logger,
    Exception primaryEx,    // ← captured, not in template
    Exception fallbackEx);  // ← must be in template
```

## Dynamic log level

Omit `Level` from the attribute and add `LogLevel level` as a parameter. Useful for configurable verbosity:

```csharp
[LoggerMessage(
    EventId = 9001,
    Message = "Cache operation {Operation} for key {Key} took {ElapsedMs}ms")]
public static partial void CacheOperation(
    ILogger logger,
    LogLevel level,         // ← position doesn't matter
    string operation,
    string key,
    long elapsedMs);

// Usage:
Log.CacheOperation(_logger,
    hit ? LogLevel.Debug : LogLevel.Warning,
    "GET", cacheKey, elapsed);
```

## SkipEnabledCheck

By default the generator wraps the call in `logger.IsEnabled(level)`. If evaluating a parameter is expensive, skip the guard in the method and add it at the call site:

```csharp
[LoggerMessage(
    Level = LogLevel.Debug,
    Message = "Payload snapshot: {Snapshot}",
    SkipEnabledCheck = true)]
public static partial void PayloadSnapshot(ILogger logger, string snapshot);

// Call site:
if (_logger.IsEnabled(LogLevel.Debug))
{
    var snapshot = BuildExpensiveSnapshot();   // only evaluated when needed
    Log.PayloadSnapshot(_logger, snapshot);
}
```

## Format specifiers

Use standard .NET format strings inside the placeholder braces:

```csharp
[LoggerMessage(
    Level = LogLevel.Information,
    Message = "Price changed to {Price:C2} after {ElapsedMs:F0}ms")]
public static partial void PriceChanged(
    ILogger logger,
    decimal price,
    double elapsedMs);
```

## Redacting sensitive data

Use `Microsoft.Extensions.Compliance.Redaction` to classify and redact sensitive parameters. This prevents PII from leaking into logs even when the method is called innocuously.

```csharp
// 1. Classify the parameter with a data-classification attribute
[LoggerMessage(
    Level = LogLevel.Information,
    Message = "User authenticated: {Email}")]
public static partial void UserAuthenticated(
    ILogger logger,
    [MyClassifications.Private] string email);    // ← redacted by the pipeline

// 2. Register the redactor at startup
services.AddRedaction(b =>
    b.SetRedactor<ErasingRedactor>(MyClassifications.Private));

// 3. Enable redaction in the logging pipeline
services.AddLogging(b => b.EnableRedaction());
```

The logged output will show `***` (or whatever the redactor produces) instead of the raw email address.

Docs: https://learn.microsoft.com/dotnet/core/extensions/logging/source-generation#redacting-sensitive-information-in-logs

## Log scopes

Source-generated methods work naturally inside `ILogger.BeginScope`. The scope is independent of the generated methods:

```csharp
using (_logger.BeginScope("RequestId={RequestId}", requestId))
{
    Log.Orders.OrderProcessing(_logger, order.Id);
    // ...
    Log.Orders.OrderCompleted(_logger, order.Id);
}
```

Enable `IncludeScopes: true` in the console logger section of your configuration for scope data to appear in console output.
