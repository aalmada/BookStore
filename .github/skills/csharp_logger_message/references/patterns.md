# LoggerMessage Organisation Patterns

## Recommended: central partial class split across files

For services with more than a handful of log messages, avoid putting everything in one file. Instead use a single `static partial class Log` split into feature-area files. This keeps files small and makes diffs readable.

### Log.cs — stub declarations

```csharp
// Infrastructure/Logging/Log.cs
using Microsoft.Extensions.Logging;

namespace MyApp.Infrastructure.Logging;

/// <summary>
/// Source-generated high-performance log messages, organised by feature area.
/// </summary>
public static partial class Log
{
    public static partial class Books { }
    public static partial class Orders { }
    public static partial class Users { }
    public static partial class Infrastructure { }
}
```

### Log.Books.cs — feature-area file

```csharp
// Infrastructure/Logging/Log.Books.cs
using Microsoft.Extensions.Logging;

namespace MyApp.Infrastructure.Logging;

public static partial class Log
{
    public static partial class Books
    {
        [LoggerMessage(
            EventId = 1001,
            Level = LogLevel.Information,
            Message = "Book created: Id={BookId}, Title={Title}")]
        public static partial void BookCreated(
            ILogger logger, Guid bookId, string title);

        [LoggerMessage(
            EventId = 1002,
            Level = LogLevel.Warning,
            Message = "Book {BookId} not found")]
        public static partial void BookNotFound(
            ILogger logger, Guid bookId);
    }
}
```

### Call site

```csharp
// Inside a handler or service
Log.Books.BookCreated(_logger, book.Id, book.Title);
```

## Flat static class (simpler projects)

When there are fewer messages or the service is small, a single file works fine:

```csharp
public static partial class Log
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Widget {WidgetId} processed")]
    public static partial void WidgetProcessed(ILogger logger, Guid widgetId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Widget {WidgetId} failed")]
    public static partial void WidgetFailed(ILogger logger, Exception ex, Guid widgetId);
}
```

## Inline on the service class (tight coupling, acceptable for small classes)

```csharp
public partial class ReportGenerator(ILogger<ReportGenerator> logger)
{
    public async Task GenerateAsync(Guid reportId)
    {
        LogGenerating(reportId);
        // ...
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Generating report {ReportId}")]
    private partial void LogGenerating(Guid reportId);
}
```

Use this when the log messages are intimately tied to one class and you don't want a separate file. The trade-off is that the messages aren't reusable across classes.

## ILogger extension methods (library-style)

Good for libraries or shared packages where consumers inject their own logger:

```csharp
internal static partial class LogMessages
{
    [LoggerMessage(
        Message = "Cache miss for key {Key}",
        Level = LogLevel.Debug)]
    internal static partial void CacheMiss(this ILogger logger, string key);
}

// Usage in any class that has ILogger:
_logger.CacheMiss(cacheKey);
```

## Naming conventions

| Context | Convention | Example |
|---------|-----------|---------|
| Static helper class | `Log` or `LogMessages` | `Log.cs` / `LogMessages.cs` |
| Feature area sub-class | `<FeatureName>` | `Log.Orders` |
| Method names | Past-tense verb or noun phrase | `BookCreated`, `ConnectionFailed`, `CacheHit` |
| Parameters | camelCase | `bookId`, `userId`, `elapsedMs` |
| Template placeholders | PascalCase | `{BookId}`, `{UserId}` |

Method names that describe what already happened (past tense) read naturally: `Log.Orders.OrderDispatched(...)` says something happened. Names like `LogOrderDispatched` also work — pick one and be consistent.
