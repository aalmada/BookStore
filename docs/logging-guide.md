# Structured Logging Guide

This guide explains the high-performance structured logging system used in the Book Store API, built with C#'s source-generated `LoggerMessage` attribute.

## Overview

The Book Store API uses **source-generated logging** for zero-allocation, high-performance structured logging. All log messages are defined using the `[LoggerMessage]` attribute, which generates optimized code at compile time.

### Key Benefits

- ✅ **Zero allocations** - No boxing, no string formatting overhead
- ✅ **Compile-time validation** - Catch errors before runtime
- ✅ **Strong typing** - Type-safe parameters
- ✅ **High performance** - Optimized code generation
- ✅ **Easy maintenance** - Centralized log definitions

## Architecture

### Organization Structure

All logging is organized in the [Infrastructure/Logging](file:///Users/antaoalmada/Projects/BookStore/src/ApiService/BookStore.ApiService/Infrastructure/Logging) directory:

```
Infrastructure/Logging/
├── Log.cs                    # Base partial class
├── Log.Books.cs             # Book-related operations
├── Log.Authors.cs           # Author-related operations
├── Log.Categories.cs        # Category-related operations
├── Log.Publishers.cs        # Publisher-related operations
└── Log.Infrastructure.cs    # Middleware & startup
```

### Partial Class Design

The logging system uses a central `Log` static partial class with nested partial classes for each feature area:

```csharp
public static partial class Log
{
    public static partial class Books { }
    public static partial class Authors { }
    public static partial class Categories { }
    public static partial class Publishers { }
    public static partial class Infrastructure { }
}
```

**Why this design?**
- 📁 **Organized by feature** - Easy to find related log messages
- 🔍 **Easy discovery** - All logs in one namespace
- 🛠️ **Maintainable** - Each file focuses on one domain area
- 📦 **Scalable** - Add new feature areas without conflicts

## Usage Examples

### Handler Logging

All command handlers include comprehensive logging for operations, validation, and errors.

#### Book Creation

See [BookHandlers.cs:29-106](file:///Users/antaoalmada/Projects/BookStore/src/ApiService/BookStore.ApiService/Handlers/Books/BookHandlers.cs#L29-L106):

```csharp
public static (IResult, BookCreatedNotification) Handle(
    CreateBook command,
    IDocumentSession session,
    IOptions<LocalizationOptions> localizationOptions,
    ILogger logger)
{
    // Log operation start with correlation ID
    Log.Books.BookCreating(logger, command.Id, command.Title, session.CorrelationId ?? "none");

    // Validation with structured logging
    if (!CultureValidator.IsValidCultureCode(command.Language))
    {
        Log.Books.InvalidLanguageCode(logger, command.Id, command.Language);
        return (Results.BadRequest(/* ... */), null!);
    }

    // ... business logic ...

    // Log successful completion
    Log.Books.BookCreated(logger, command.Id, command.Title);
    
    return (Results.Created(/* ... */), notification);
}
```

**Structured Properties:**
- `BookId` - Entity identifier for correlation
- `Title` - Human-readable context
- `CorrelationId` - Distributed tracing support

#### Validation Logging

Validation failures include detailed context:

```csharp
if (translation.Description.Length > BookAggregate.MaxDescriptionLength)
{
    Log.Books.DescriptionTooLong(
        logger,
        command.Id,
        languageCode,
        BookAggregate.MaxDescriptionLength,
        translation.Description.Length);
    
    return (Results.BadRequest(/* ... */), null!);
}
```

**Structured Properties:**
- `BookId` - Which book failed validation
- `LanguageCode` - Which translation failed
- `MaxLength` - Expected limit
- `ActualLength` - Actual value for debugging

### Middleware Logging

#### Marten Metadata

See [MartenMetadataMiddleware.cs:50](file:///Users/antaoalmada/Projects/BookStore/src/ApiService/BookStore.ApiService/Infrastructure/MartenMetadataMiddleware.cs#L50):

```csharp
// Log the Marten metadata setup
Log.Infrastructure.MartenMetadataSet(
    _logger,
    correlationId,
    causationId,
    userId ?? "anonymous");
```

#### Request Tracking

See [LoggingEnricher.cs:40-46](file:///Users/antaoalmada/Projects/BookStore/src/ApiService/BookStore.ApiService/Infrastructure/LoggingEnricher.cs#L40-L46):

```csharp
// Log the request start with enriched metadata
Log.Infrastructure.RequestStarted(
    _logger,
    requestMethod,
    requestPath ?? "/",
    remoteIp ?? "unknown");
```

### Startup Logging

#### Projection Initialization

See [Program.cs:46-71](file:///Users/antaoalmada/Projects/BookStore/src/ApiService/BookStore.ApiService/Program.cs#L46-L71):

```csharp
static async Task WaitForProjectionsAsync(IDocumentStore store, ILogger logger)
{
    Log.Infrastructure.WaitingForProjections(logger);

    // ... polling logic ...

    if (bookCount > 0 && authorCount > 0 && categoryCount > 0 && publisherCount > 0)
    {
        Log.Infrastructure.ProjectionsReady(
            logger,
            bookCount,
            authorCount,
            categoryCount,
            publisherCount);
        return;
    }

    // ... timeout handling ...
    
    Log.Infrastructure.ProjectionTimeout(logger, timeout.TotalSeconds);
}
```

## Log Levels

The system uses appropriate log levels for different scenarios:

| Level | Usage | Examples |
|-------|-------|----------|
| **Debug** | Detailed diagnostic information | Request details, projection status checks |
| **Information** | Normal operations | Entity created, updated, deleted, restored |
| **Warning** | Validation failures, not found | Invalid language code, ETag mismatch, entity not found |
| **Error** | Unexpected failures | Database errors, seeding failures |

### Level Guidelines

**Debug:**
- Use for detailed diagnostic information
- Typically disabled in production
- Examples: Request metadata, query parameters

**Information:**
- Use for successful operations
- Track normal application flow
- Examples: Entity created, projection ready

**Warning:**
- Use for expected failures
- Validation errors, not found scenarios
- Examples: Invalid input, missing entity

**Error:**
- Use for unexpected failures
- System errors, exceptions
- Examples: Database connection failure, unhandled exceptions

## Defining New Log Messages

### Step 1: Choose the Right File

Add log messages to the appropriate partial class:
- **Books** → `Log.Books.cs`
- **Authors** → `Log.Authors.cs`
- **Categories** → `Log.Categories.cs`
- **Publishers** → `Log.Publishers.cs`
- **Infrastructure** → `Log.Infrastructure.cs`

### Step 2: Define the Log Message

```csharp
[LoggerMessage(
    Level = LogLevel.Information,
    Message = "Book published: Id={BookId}, Title={Title}, PublishDate={PublishDate}")]
public static partial void BookPublished(
    ILogger logger,
    Guid bookId,
    string title,
    DateOnly publishDate);
```

**Key points:**
- Use descriptive method names (e.g., `BookPublished`, not `LogBookPublish`)
- Include relevant structured properties in the message template
- Use appropriate log level
- Make the method `static partial void`

### Step 3: Use in Your Code

```csharp
public static IResult Handle(PublishBook command, IDocumentSession session, ILogger logger)
{
    // ... business logic ...
    
    Log.Books.BookPublished(logger, command.Id, book.Title, command.PublishDate);
    
    return Results.Ok();
}
```

## Structured Properties

### Naming Conventions

Use **PascalCase** for property names in log messages:
- ✅ `BookId`, `Title`, `CorrelationId`
- ❌ `bookId`, `book_id`, `BOOK_ID`

### Common Properties

Include these properties for better observability:

**Entity Operations:**
- `{EntityType}Id` - Entity identifier (e.g., `BookId`, `AuthorId`)
- `CorrelationId` - Request correlation for distributed tracing
- `Version` - Entity version for concurrency tracking

**Validation:**
- `LanguageCode` - Which language/culture failed
- `MaxLength` / `ActualLength` - Validation limits
- `InvalidCodes` - List of invalid values

**Requests:**
- `RequestPath` - HTTP path
- `RequestMethod` - HTTP method
- `UserId` - Authenticated user
- `RemoteIp` - Client IP address

## Querying Logs

### Using Aspire Dashboard

The Aspire Dashboard provides powerful log querying:

1. **Navigate to Logs** in the Aspire Dashboard
2. **Filter by structured properties:**
   ```
   BookId = "550e8400-e29b-41d4-a716-446655440000"
   ```
3. **Combine filters:**
   ```
   Level = "Warning" AND Message LIKE "%validation%"
   ```
4. **Track correlation:**
   ```
   CorrelationId = "txn-12345"
   ```

### Common Queries

**Find all operations for a specific book:**
```
BookId = "550e8400-e29b-41d4-a716-446655440000"
```

**Find all validation failures:**
```
Level = "Warning" AND (
    Message LIKE "%Invalid%" OR 
    Message LIKE "%too long%" OR 
    Message LIKE "%mismatch%"
)
```

**Track a request flow:**
```
CorrelationId = "01JGXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX"
```

**Find projection issues:**
```
Message LIKE "%projection%" AND Level IN ("Warning", "Error")
```

## Performance Characteristics

### Zero-Allocation Logging

Source-generated logging avoids common allocation sources:

**Traditional logging:**
```csharp
// ❌ Allocates: string interpolation, boxing, params array
logger.LogInformation($"Book created: {bookId}, {title}");
```

**Source-generated logging:**
```csharp
// ✅ Zero allocations: optimized at compile time
Log.Books.BookCreated(logger, bookId, title);
```

### Benchmarks

Compared to traditional logging:
- **~3x faster** for simple messages
- **~10x faster** for messages with multiple parameters
- **Zero allocations** in most scenarios
- **Compile-time validation** prevents runtime errors

### When Allocations Occur

Allocations may still occur for:
- **String concatenation** in message templates
- **Collection formatting** (e.g., `string.Join`)
- **Exception logging** (exception objects)

These are typically unavoidable and acceptable trade-offs.

## Best Practices

### ✅ Do

- **Use structured properties** instead of string interpolation
- **Log at appropriate levels** (Debug for diagnostics, Information for operations)
- **Include correlation IDs** for distributed tracing
- **Log both success and failure** paths
- **Use descriptive method names** that explain what happened
- **Keep messages concise** but informative

### ❌ Don't

- **Don't log sensitive data** (passwords, tokens, PII)
- **Don't log in tight loops** (use sampling or aggregation)
- **Don't use string interpolation** in log messages
- **Don't log redundant information** already in structured properties
- **Don't use generic messages** like "Error occurred"

### Example: Good vs Bad

**❌ Bad:**
```csharp
logger.LogInformation($"Book {bookId} was created with title {title}");
// Problems: string interpolation, no structured properties, verbose
```

**✅ Good:**
```csharp
Log.Books.BookCreated(logger, bookId, title);
// Benefits: zero allocations, structured properties, concise
```

## Integration with Observability

### OpenTelemetry

The logging system integrates with OpenTelemetry for distributed tracing:

- **Correlation IDs** link logs across services
- **Trace IDs** connect logs to distributed traces
- **Span IDs** associate logs with specific operations

See [correlation-causation-guide.md](file:///Users/antaoalmada/Projects/BookStore/docs/correlation-causation-guide.md) for more details.

### Aspire Dashboard

All structured logs are automatically sent to the Aspire Dashboard:

- **Real-time log streaming**
- **Structured property filtering**
- **Correlation ID tracking**
- **Log level filtering**
- **Full-text search**

## Troubleshooting

### Log Messages Not Appearing

**Problem:** Log messages defined but not showing in output.

**Solutions:**
1. **Check log level** - Ensure the log level is enabled in configuration
2. **Verify logger injection** - Ensure `ILogger` is injected into the handler
3. **Rebuild project** - Source generators run during compilation
4. **Check using statement** - Ensure `using BookStore.ApiService.Infrastructure.Logging;`

### Compilation Errors

**Problem:** `CS0117: 'Log.Books' does not contain a definition for 'BookCreated'`

**Solutions:**
1. **Rebuild the project** - Source generator needs to run
2. **Check method signature** - Ensure parameters match the definition
3. **Verify namespace** - Ensure the partial class is in the correct namespace
4. **Clean and rebuild** - `dotnet clean && dotnet build`

### Performance Issues

**Problem:** Logging is causing performance degradation.

**Solutions:**
1. **Check log level** - Disable Debug logging in production
2. **Review logging frequency** - Don't log in tight loops
3. **Use sampling** - Log only a percentage of high-frequency events
4. **Verify source generation** - Ensure you're using `[LoggerMessage]` attribute

## Related Documentation

- [Performance Guide](file:///Users/antaoalmada/Projects/BookStore/docs/performance-guide.md) - GC optimization, tiered compilation
- [Correlation & Causation Guide](file:///Users/antaoalmada/Projects/BookStore/docs/correlation-causation-guide.md) - Distributed tracing
- [Microsoft Docs: LoggerMessage](https://learn.microsoft.com/en-us/dotnet/core/extensions/logger-message-generator) - Official documentation

## Summary

The Book Store API's structured logging system provides:

- ✅ **High performance** - Zero-allocation logging via source generation
- ✅ **Strong typing** - Compile-time validation of log messages
- ✅ **Easy maintenance** - Centralized, organized log definitions
- ✅ **Great observability** - Structured properties for powerful querying
- ✅ **Production-ready** - Integrated with OpenTelemetry and Aspire

By following the patterns and best practices in this guide, you can add effective, performant logging to any part of the application.
