# LoggerMessage Basics

Source: https://learn.microsoft.com/dotnet/core/extensions/logging/source-generation

## How it works

Placing `[LoggerMessage]` on a `partial void` method triggers the C# source generator at compile time. The generator emits an implementation that calls `ILogger.Log` with a pre-computed `EventDefinition` — no boxing, no runtime parsing.

Available from .NET 6+. Requires C# 9+.

## Attribute properties

| Property | Type | Required | Notes |
|----------|------|----------|-------|
| `EventId` | `int` | No | Unique per-component identifier; recommended for structured logging sinks |
| `Level` | `LogLevel` | No* | Omit to make the level dynamic (passed at call site) |
| `Message` | `string` | No | Message template; omitting yields `String.Empty` |
| `EventName` | `string` | No | Overrides the auto-derived name in sinks that surface it |
| `SkipEnabledCheck` | `bool` | No | Suppresses the generated `IsEnabled` guard — use when evaluation of parameters is expensive and you want to guard manually |

*If you omit `Level`, you must add a `LogLevel level` parameter to the method.

## Method constraints

The following rules produce compile errors or warnings if violated:

- Method must be `partial` and return `void`.
- Method name must **not** start with `_`.
- Parameter names must **not** start with `_`.
- `params`, `scoped`, `out` modifiers and `ref struct` types are not allowed on parameters.
- `allows ref struct` anti-constraint (C# 13) is not supported.
- Static methods **must** accept `ILogger` as a parameter. Instance methods read it from a field or primary constructor parameter.

## Static method (most common)

```csharp
public static partial class Log
{
    [LoggerMessage(
        EventId = 42,
        Level = LogLevel.Warning,
        Message = "Retry {Attempt} of {Max} for {OperationName}")]
    public static partial void RetryAttempt(
        ILogger logger,
        int attempt,
        int max,
        string operationName);
}
```

## Extension method variant

Adding `this` turns it into an `ILogger` extension method, which reads more naturally at the call site:

```csharp
public static partial class Log
{
    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Order {OrderId} dispatched")]
    public static partial void OrderDispatched(this ILogger logger, Guid orderId);
}

// Usage:
_logger.OrderDispatched(order.Id);
```

## Instance method

When a class holds `ILogger` as a field (or .NET 9+ primary constructor parameter), you can declare the log method directly on the class:

```csharp
public partial class OrderService(ILogger<OrderService> logger)
{
    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Processing order {OrderId}")]
    private partial void LogProcessingOrder(Guid orderId);
}
```

The generator reads `logger` from the primary constructor parameter automatically (.NET 9+). For earlier versions, use a field:

```csharp
public partial class OrderService
{
    private readonly ILogger _logger;

    public OrderService(ILogger<OrderService> logger) => _logger = logger;

    [LoggerMessage(Level = LogLevel.Information, Message = "Processing order {OrderId}")]
    private partial void LogProcessingOrder(Guid orderId);
}
```

## Template placeholder naming

Use **PascalCase** for placeholder names to match structured-logging conventions and to produce clean JSON property names in sinks like Seq or Application Insights:

```
✅ "Book {BookId} created by tenant {TenantId}"
❌ "Book {bookId} created by tenant {tenantId}"
```

The comparison between placeholder and parameter name is case-insensitive, so `{BookId}` matches a parameter named `bookId` or `BookId`.

## EventId ranges

When assigning event IDs, adopt consistent ranges per feature area so IDs are unique and easy to filter in any log management tool:

```
1000–1999  Books
2000–2999  Authors / Publishers
3000–3999  Users / Auth
4000–4999  Orders / Payments
5000–5999  Infrastructure
6000–6999  Notifications / SSE
```

EventId is optional but strongly recommended when logs are shipped to any structured sink.
