# Structured Logging Guide

This guide explains the high-performance structured logging system used in the BookStore API, built with C#'s source-generated `LoggerMessage` attribute.

## Overview

The BookStore API uses **source-generated logging** for zero-allocation, high-performance structured logging. All log messages are defined using the `[LoggerMessage]` attribute, which generates optimized code at compile time.

### Key Benefits

- ✅ **Zero allocations** - No boxing, no string formatting overhead
- ✅ **Compile-time validation** - Catch errors before runtime
- ✅ **Strong typing** - Type-safe parameters
- ✅ **High performance** - Optimized code generation
- ✅ **Easy maintenance** - Centralized log definitions

> **Mandatory rule:** `_logger.LogInformation(...)`, `_logger.LogWarning(...)`, and similar extension methods are **never** used directly. Always use `[LoggerMessage]` source-generated methods. Analyzer rule **CA1848** enforces this.

## Architecture

### Organization Structure

All logging is organized in [src/BookStore.ApiService/Infrastructure/Logging/](../../src/BookStore.ApiService/Infrastructure/Logging/):

```
Infrastructure/Logging/
├── Log.cs                    # Base partial class — declares all nested class stubs
├── Log.Books.cs             # Book-related operations
├── Log.Authors.cs           # Author-related operations
├── Log.Categories.cs        # Category-related operations
├── Log.Publishers.cs        # Publisher-related operations
├── Log.Infrastructure.cs    # Middleware, startup, projections, Marten metadata
├── Log.Email.cs             # Email delivery operations
├── Log.Maintenance.cs       # Background maintenance jobs
├── Log.Notifications.cs     # SSE notification service
├── Log.Seeding.cs           # Database seeding
├── Log.Tenants.cs           # Multi-tenancy access control
└── Log.Users.cs             # User authentication/registration
```

### Partial Class Design

`Log.cs` declares the central `Log` static partial class and all nested class stubs. Each feature-area file extends one nested class with its `[LoggerMessage]` methods:

```csharp
// Log.cs — declares stubs
public static partial class Log
{
    public static partial class Books { }
    public static partial class Authors { }
    public static partial class Categories { }
    public static partial class Publishers { }
    public static partial class Infrastructure { }
    public static partial class Email { }
    public static partial class Users { }
    public static partial class Tenants { }
}
```

Additional feature areas (`Notifications`, `Maintenance`, `Seeding`) are extended directly in their own files without a stub in `Log.cs` — the C# compiler merges them automatically:

```csharp
// Log.Notifications.cs — extends Log without a stub in Log.cs
public static partial class Log
{
    public static partial class Notifications
    {
        [LoggerMessage(EventId = 6002, Level = LogLevel.Information,
            Message = "Client {SubscriberId} subscribed. Total subscribers: {Count}")]
        public static partial void ClientSubscribed(ILogger logger, Guid subscriberId, int count);
    }
}
```

**Why this design?**
- 📁 **Organized by feature** - Easy to find related log messages
- 🔍 **Easy discovery** - All logs under `Log.*`
- 🛠️ **Maintainable** - Each file focuses on one domain area
- 📦 **Scalable** - Add new feature areas without touching existing files

## Usage Examples

### Handler Logging

All command handlers receive `ILogger` as a parameter and call the appropriate `Log.*` method.

#### Book Creation

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

```csharp
// Log the Marten metadata setup
Log.Infrastructure.MartenMetadataApplied(
    _logger,
    method,
    path,
    correlationId,
    causationId,
    userId,
    remoteIp);
```

#### Request Tracking

The `LoggingEnricherMiddleware` automatically enriches the log scope for every HTTP request with:
- `CorrelationId` (captured from headers or generated)
- `CausationId` (captured from headers or root request ID)
- `TraceId`, `SpanId` (from OpenTelemetry)
- `UserId`, `RemoteIp`, `UserAgent`

```csharp
// Log the request start with enriched metadata
Log.Infrastructure.RequestStarted(logger, method, path, remoteIp);
```

### Startup Logging

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

    Log.Infrastructure.ProjectionTimeout(logger, timeout.TotalSeconds);
}
```

## Log Levels

The system uses appropriate log levels for different scenarios:

| Level | Usage | Examples |
|-------|-------|----------|
| **Debug** | Detailed diagnostic information | Request details, projection status checks, cache invalidations |
| **Information** | Normal operations | Entity created, updated, deleted, restored, seeding completed |
| **Warning** | Validation failures, configuration advisories | Invalid language code, ETag mismatch, entity not found, HS256 in non-dev |
| **Error** | Unexpected failures | Database errors, email delivery failures, projection commit errors |
| **Critical** | Fatal conditions requiring immediate attention | Failed Marten event registration, rate limiting disabled in production |

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
- Examples: Database connection failure, email delivery failure, unhandled exceptions

**Critical:**
- Use for fatal conditions that compromise system integrity
- Examples: Failed Marten event registration, rate limiting disabled in non-development

## Defining New Log Messages

### Step 1: Choose the Right File

Add log messages to the appropriate partial class file:
- **Books** → `Log.Books.cs`
- **Authors** → `Log.Authors.cs`
- **Categories** → `Log.Categories.cs`
- **Publishers** → `Log.Publishers.cs`
- **Infrastructure** → `Log.Infrastructure.cs` (middleware, startup, projections)
- **Email** → `Log.Email.cs`
- **Maintenance** → `Log.Maintenance.cs`
- **Notifications** → `Log.Notifications.cs`
- **Seeding** → `Log.Seeding.cs`
- **Tenants** → `Log.Tenants.cs`
- **Users** → `Log.Users.cs`

If none fits, create a new `Log.<Area>.cs` file following the same pattern.

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
- Return type must be `void`
- Method must be `static partial`
- Declaring class must be `partial`
- Use **PascalCase** placeholders: `{BookId}`, not `{bookId}`
- `Exception` must be the **first** positional parameter after `ILogger` (treated specially by the runtime)
- Do **not** use string interpolation in `Message` — placeholders only

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
- `UserId` - Authenticated user (GUID only — see PII section)
- `RemoteIp` - Client IP address

## Log Level Configuration

### appsettings.json

The API service configures log levels in `src/BookStore.ApiService/appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.AspNetCore.Hosting": "Information",
      "Microsoft.AspNetCore.Routing": "Warning",
      "System.Net.Http.HttpClient": "Warning",
      "Marten": "Warning",
      "Npgsql": "Warning",
      "Wolverine": "Warning",
      "BookStore": "Information"
    }
  }
}
```

- `BookStore.*` namespaces log at `Information` and above
- Third-party libraries (`Marten`, `Npgsql`, `Wolverine`) are set to `Warning` to reduce noise
- Override individual categories in `appsettings.Development.json` for local debugging

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
Level = "Warning"
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

**Traditional logging — avoid:**
```csharp
// ❌ Allocates: string interpolation, boxing, params array
logger.LogInformation($"Book created: {bookId}, {title}");
```

**Source-generated logging — correct:**
```csharp
// ✅ Zero allocations: optimized at compile time
Log.Books.BookCreated(logger, bookId, title);
```

### When Allocations Occur

Minor allocations may still occur for:
- **Exception logging** (exception objects)
- **Collection formatting** (e.g., `string.Join` in message parameters)

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

- **Don't call `_logger.Log*()` extension methods directly** — always use `[LoggerMessage]`
- **Don't log sensitive data** (passwords, tokens, cryptographic keys)
- **Don't log in tight loops** (use sampling or aggregation)
- **Don't use string interpolation** in log messages
- **Don't log redundant information** already in structured properties
- **Don't use generic messages** like "Error occurred"

## PII Privacy Policy

To comply with privacy regulations (like GDPR) and maintain a clean audit trail, the application follows a strict **No PII in Logs/Metadata** policy:

1. **User Identification**: Always use the user's **GUID ID** (from `ClaimTypes.NameIdentifier`) instead of email or username where possible. This ensures that even if logs are leaked, users cannot be directly identified without access to the primary database.
2. **Metadata Capture**: When capturing technical metadata for events (IP, User-Agent), ensure these reflect the original client while avoiding storage of PII in log fields.
3. **Sensitive Data**: Never log passwords, reset tokens, or cryptographic keys.

## OpenTelemetry Integration

### Configuration (ServiceDefaults)

`BookStore.ServiceDefaults` configures logging and telemetry for all services via `AddServiceDefaults()` → `ConfigureOpenTelemetry()`:

```csharp
// Structured log export to OTel
builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
});

// Development: human-readable simple console
// Production: JSON console (machine-parseable)
builder.Logging.AddConsole(options =>
    options.FormatterName = builder.Environment.IsDevelopment() ? "simple" : "json");
```

**Development output** (`simple` formatter):
- Single-line human-readable entries
- UTC timestamps
- Scopes included

**Production output** (`json` formatter):
- JSON structured output
- UTC ISO-8601 timestamps (`yyyy-MM-ddTHH:mm:ss.fffZ`)
- Scopes included for correlation

### OTLP Exporter

When `OTEL_EXPORTER_OTLP_ENDPOINT` is set (automatic with Aspire), all logs/traces/metrics are exported via OTLP to the Aspire Dashboard or an external collector:

```csharp
if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
{
    builder.Services.AddOpenTelemetry().UseOtlpExporter();
}
```

### Metrics and Tracing Sources

The telemetry pipeline includes:
- **Metrics:** ASP.NET Core, HttpClient, Runtime, `Wolverine`, `BookStore.ApiService`
- **Tracing:** ASP.NET Core (health-check paths excluded), HttpClient, `Wolverine`

Health check paths (`/health`, `/alive`) are excluded from tracing to reduce noise.

### Aspire Dashboard

All structured logs are automatically sent to the Aspire Dashboard:

- **Real-time log streaming**
- **Structured property filtering**
- **Correlation ID tracking**
- **Log level filtering**
- **Full-text search**

### Correlation IDs

The logging system integrates with OpenTelemetry for distributed tracing:

- **Correlation IDs** link logs across services and requests
- **Trace IDs** connect logs to distributed traces
- **Span IDs** associate logs with specific operations

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
3. **Verify source generation** - Ensure you're using `[LoggerMessage]` attribute, not extension methods

## Related Documentation

- [Correlation & Causation Guide](correlation-causation-guide.md) - Distributed tracing
- [Microsoft Docs: LoggerMessage source generator](https://learn.microsoft.com/en-us/dotnet/core/extensions/logger-message-generator) - Official documentation
- [OpenTelemetry .NET](https://opentelemetry.io/docs/languages/dotnet/) - OTel SDK documentation

## Summary

The BookStore API's structured logging system provides:

- ✅ **High performance** — Zero-allocation logging via source generation
- ✅ **Strong typing** — Compile-time validation of log messages
- ✅ **Easy maintenance** — Centralized, organized log definitions across 12 feature-area files
- ✅ **Great observability** — Structured properties for powerful querying
- ✅ **Production-ready** — Integrated with OpenTelemetry, Aspire Dashboard, and OTLP export

By following the patterns and best practices in this guide, you can add effective, performant logging to any part of the application.
