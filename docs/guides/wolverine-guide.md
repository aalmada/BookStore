# Wolverine Integration Guide

## Overview

The Book Store API uses **Wolverine** - a next-generation .NET mediator and message bus framework - to implement the command/handler pattern with automatic transaction management.

## Why Wolverine?

### Benefits

✅ **Clean Separation**: Business logic separated from HTTP concerns  
✅ **Testability**: Handlers are pure functions, easy to unit test  
✅ **Auto-Transactions**: Wolverine manages Marten sessions and commits  
✅ **No Boilerplate**: No manual `SaveChangesAsync()` calls  
✅ **Foundation for Async**: Ready for background jobs and messaging  

### Before/After Comparison

**Before (Traditional Endpoint)**:
```csharp
private static async Task<IResult> CreateBook(
    CreateBookRequest request,
    IDocumentSession session)
{
    var id = Guid.CreateVersion7();
    var @event = BookAggregate.Create(...);
    session.Events.StartStream<BookAggregate>(id, @event);
    await session.SaveChangesAsync(); // Manual transaction management
    return Results.Created($"/api/admin/books/{id}", new { id });
}
```

**After (Wolverine Pattern)**:
```csharp
// Endpoint: Just routes to command
private static Task<IResult> CreateBook(
    CreateBookRequest request,
    IMessageBus bus)
{
    var command = new CreateBook(...);
    return bus.InvokeAsync<IResult>(command);
}

// Handler: Pure, testable business logic
public static IResult Handle(CreateBook command, IDocumentSession session)
{
    var @event = BookAggregate.Create(...);
    session.Events.StartStream<BookAggregate>(command.Id, @event);
    // Wolverine auto-commits transaction
    return Results.Created(...);
}
```

## Architecture

### Command Flow

```
```mermaid
sequenceDiagram
    participant Client
    participant Endpoint
    participant Bus as IMessageBus
    participant Handler
    participant Marten as Marten/DB
    
    Client->>Endpoint: 1. HTTP Request
    Endpoint->>Bus: 2. InvokeAsync(Command)
    Bus->>Handler: 3. Route to Handler
    Handler->>Handler: 4. Execute Logic
    Handler->>Marten: 5. Store Events
    Bus->>Marten: 6. Auto-commit Transaction
    Endpoint->>Client: 7. Return Result
```
```

### Project Structure

```
BookStore.ApiService/
├── Commands/
│   └── Books/
│       └── BookCommands.cs      # Command records
├── Handlers/
│   └── Books/
│       └── BookHandlers.cs      # Handler static methods
└── Endpoints/
    └── Admin/
        └── AdminBookEndpoints.cs # Thin routing layer
```

## Creating Commands

Commands are immutable records that represent user intent. The BookStore project features commands with localization, multi-currency, ETags, and sales management:

```csharp
namespace BookStore.ApiService.Commands;

/// <summary>
/// Command to create a new book
/// </summary>
public record CreateBook(
    string Title,
    string? Isbn,
    string Language,
    IReadOnlyDictionary<string, BookTranslationDto>? Translations,
    PartialDate? PublicationDate,
    Guid? PublisherId,
    IReadOnlyList<Guid> AuthorIds,
    IReadOnlyList<Guid> CategoryIds,
    IReadOnlyDictionary<string, decimal>? Prices = null)
{
    /// <summary>
    /// Unique identifier for the book (generated automatically)
    /// </summary>
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

/// <summary>
/// DTO for localized book descriptions
/// </summary>
public record BookTranslationDto(string Description);

/// <summary>
/// Command to update an existing book
/// </summary>
public record UpdateBook(
    Guid Id,
    string Title,
    string? Isbn,
    string Language,
    IReadOnlyDictionary<string, BookTranslationDto>? Translations,
    PartialDate? PublicationDate,
    Guid? PublisherId,
    IReadOnlyList<Guid> AuthorIds,
    IReadOnlyList<Guid> CategoryIds,
    IReadOnlyDictionary<string, decimal>? Prices = null)
{
    /// <summary>
    /// ETag for optimistic concurrency control
    /// </summary>
    public string? ETag { get; init; }
}

/// <summary>
/// Command to soft delete a book
/// </summary>
public record SoftDeleteBook(Guid Id)
{
    public string? ETag { get; init; }
}

/// <summary>
/// Command to restore a soft deleted book
/// </summary>
public record RestoreBook(Guid Id)
{
    public string? ETag { get; init; }
}

/// <summary>
/// Command to schedule a sale for a book
/// </summary>
public record ScheduleBookSale(
    Guid BookId,
    decimal Percentage,
    DateTimeOffset Start,
    DateTimeOffset End)
{
    public string? ETag { get; init; }
}

/// <summary>
/// Command to cancel a scheduled sale
/// </summary>
public record CancelBookSale(
    Guid BookId,
    DateTimeOffset SaleStart)
{
    public string? ETag { get; init; }
}
```

**Command Best Practices**:
- Use `record` for immutability
- Include all required data
- Use `init` for optional properties (like ETag)
- Add XML documentation
- Keep commands simple (no logic)
- Use `Guid.CreateVersion7()` for IDs (see [Marten Guide](marten-guide.md#performance-guidcreateversion7) for performance benefits)
- **Localization**: Use `IReadOnlyDictionary<string, BookTranslationDto>` for multilingual content
- **Multi-Currency**: Use `IReadOnlyDictionary<string, decimal>` for prices in multiple currencies

## Creating Handlers

> [!NOTE]
> Handlers use Marten's event sourcing APIs. For details on streams, aggregates, and events, see the [Marten Guide](marten-guide.md).

Handlers are static methods that Wolverine auto-discovers. The BookStore handlers demonstrate real-world patterns like configuration validation, localization/currency checks, and structured logging:

```csharp
namespace BookStore.ApiService.Handlers.Books;

public static class BookHandlers
{
    /// <summary>
    /// Handle CreateBook command
    /// Wolverine automatically manages the Marten session and commits the transaction
    /// </summary>
    public static IResult Handle(
        CreateBook command,
        IDocumentSession session,
        IOptions<LocalizationOptions> localizationOptions,
        IOptions<CurrencyOptions> currencyOptions,
        ILogger logger)
    {
        Log.Books.BookCreating(logger, command.Id, command.Title, session.CorrelationId ?? "none");
        
        // Validate language code
        if (!CultureValidator.IsValidCultureCode(command.Language))
        {
            Log.Books.InvalidLanguageCode(logger, command.Id, command.Language);
            return Results.BadRequest(new
            {
                error = "Invalid language code",
                languageCode = command.Language,
                message = $"The language code '{command.Language}' is not valid. Must be a valid ISO 639-1 (e.g., 'en'), ISO 639-3 (e.g., 'fil'), or culture code (e.g., 'en-US')"
            });
        }
        
        // Validate language codes in descriptions
        if (command.Translations?.Count > 0)
        {
            if (!CultureValidator.ValidateTranslations(command.Translations, out var invalidCodes))
            {
                Log.Books.InvalidTranslationCodes(logger, command.Id, string.Join(", ", invalidCodes));
                return Results.BadRequest(new
                {
                    error = "Invalid language codes in descriptions",
                    invalidCodes,
                    message = $"The following language codes are not valid: {string.Join(", ", invalidCodes)}"
                });
            }
        }
        
        // Validate default language translation is provided
        var defaultLanguage = localizationOptions.Value.DefaultCulture;
        if (command.Translations is null || !command.Translations.ContainsKey(defaultLanguage))
        {
            Log.Books.MissingDefaultTranslation(logger, command.Id, defaultLanguage);
            return Results.BadRequest(new
            {
                error = "Default language translation required",
                message = $"A description translation for the default language '{defaultLanguage}' must be provided"
            });
        }
        
        // Validate default currency price is provided
        var defaultCurrency = currencyOptions.Value.DefaultCurrency;
        if (command.Prices is null || !command.Prices.ContainsKey(defaultCurrency))
        {
            return Results.BadRequest(new
            {
                error = "Default currency price required",
                message = $"A price for the default currency '{defaultCurrency}' must be provided"
            });
        }
        
        // Convert DTOs to domain objects
        var descriptions = command.Translations.ToDictionary(
            kvp => kvp.Key,
            kvp => new BookTranslation(kvp.Value.Description));
        
        try
        {
            var @event = BookAggregate.CreateEvent(
                command.Id, command.Title, command.Isbn, command.Language,
                descriptions, command.PublicationDate, command.PublisherId,
                [.. command.AuthorIds], [.. command.CategoryIds],
                command.Prices?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? []);
            
            session.Events.StartStream<BookAggregate>(command.Id, @event);
        }
        catch (ArgumentException ex)
        {
            Log.Books.InvalidBookData(logger, command.Id, ex.Message);
            return Results.BadRequest(new { error = ex.Message });
        }
        
        Log.Books.BookCreated(logger, command.Id, command.Title);
        
        // Wolverine automatically calls SaveChangesAsync and publishes the event
        return Results.Created(
            $"/api/admin/books/{command.Id}",
            new { id = command.Id, correlationId = session.CorrelationId });
    }
    
    /// <summary>
    /// Handle UpdateBook command with ETag validation
    /// </summary>
    public static async Task<IResult> Handle(
        UpdateBook command,
        IDocumentSession session,
        IHttpContextAccessor contextAccessor,
        IOptions<LocalizationOptions> localizationOptions,
        IOptions<CurrencyOptions> currencyOptions,
        ILogger logger)
    {
        var context = contextAccessor.HttpContext!;
        
        // Validate language and currency (same validation as Create)
        // ... validation code omitted for brevity ...
        
        // Get current stream state for ETag validation
        var streamState = await session.Events.FetchStreamStateAsync(command.Id);
        if (streamState is null)
        {
            Log.Books.BookNotFound(logger, command.Id);
            return Results.NotFound();
        }
        
        var currentETag = ETagHelper.GenerateETag(streamState.Version);
        
        // Check If-Match header for optimistic concurrency
        if (!string.IsNullOrEmpty(command.ETag) &&
            !ETagHelper.CheckIfMatch(context, currentETag))
        {
            Log.Books.ETagMismatch(logger, command.Id, currentETag, command.ETag);
            return ETagHelper.PreconditionFailed();
        }
        
        var aggregate = await session.Events.AggregateStreamAsync<BookAggregate>(command.Id);
        if (aggregate is null)
        {
            Log.Books.BookNotFound(logger, command.Id);
            return Results.NotFound();
        }
        
        Log.Books.BookUpdating(logger, command.Id, command.Title, streamState.Version);
        
        // Convert DTOs to domain objects
        var descriptions = command.Translations.ToDictionary(
            kvp => kvp.Key,
            kvp => new BookTranslation(kvp.Value.Description));
        
        try
        {
            var @event = aggregate.UpdateEvent(
                command.Title, command.Isbn, command.Language,
                descriptions, command.PublicationDate, command.PublisherId,
                [.. command.AuthorIds], [.. command.CategoryIds],
                command.Prices?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? []);
            
            session.Events.Append(command.Id, @event);
        }
        catch (ArgumentException ex)
        {
            Log.Books.InvalidBookData(logger, command.Id, ex.Message);
            return Results.BadRequest(new { error = ex.Message });
        }
        
        Log.Books.BookUpdated(logger, command.Id);
        
        // Get new stream state and return new ETag
        var newStreamState = await session.Events.FetchStreamStateAsync(command.Id);
        var newETag = ETagHelper.GenerateETag(newStreamState!.Version);
        ETagHelper.AddETagHeader(context, newETag);
        
        return Results.NoContent();
    }
}
```

**Handler Best Practices**:
- Use static methods in static classes
- Method name must be `Handle`
- Wolverine discovers handlers by convention
- Return `IResult` for HTTP responses
- Inject dependencies as parameters (`IOptions<T>`, `ILogger`, etc.)
- No manual transaction management
- **Configuration Validation**: Check `LocalizationOptions` and `CurrencyOptions` to ensure required data is provided
- **Structured Logging**: Use `Log.Books.*` methods for consistent, high-performance logging
- **Error Handling**: Return descriptive `BadRequest` responses with error details

### Handler Discovery

Wolverine auto-discovers handlers based on:
1. **Method name**: Must be `Handle`
2. **First parameter**: The command type
3. **Return type**: Determines behavior
4. **Additional parameters**: Auto-injected by Wolverine

## Updating Endpoints

Endpoints become thin routing layers:

```csharp
using Wolverine;

public static class AdminBookEndpoints
{
    public static RouteGroupBuilder MapAdminBookEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/", CreateBook)
            .WithName("CreateBook")
            .WithSummary("Create a new book using Wolverine command/handler pattern");

        group.MapPut("/{id:guid}", UpdateBook)
            .WithName("UpdateBook")
            .WithSummary("Update a book. Supports optimistic concurrency with If-Match header.");

        return group;
    }

    private static Task<IResult> CreateBook(
        [FromBody] CreateBookRequest request,
        [FromServices] IMessageBus bus)
    {
        var command = new CreateBook(
            request.Title,
            request.Isbn,
            request.Description,
            request.PublicationDate,
            request.PublisherId,
            request.AuthorIds ?? [],
            request.CategoryIds ?? []);
        
        // Wolverine invokes the handler, manages transaction, and returns result
        return bus.InvokeAsync<IResult>(command);
    }

    private static Task<IResult> UpdateBook(
        Guid id,
        [FromBody] UpdateBookRequest request,
        [FromServices] IMessageBus bus,
        HttpContext context)
    {
        // Extract ETag from If-Match header
        var etag = context.Request.Headers["If-Match"].FirstOrDefault();
        
        var command = new UpdateBook(
            id,
            request.Title,
            request.Isbn,
            request.Description,
            request.PublicationDate,
            request.PublisherId,
            request.AuthorIds ?? [],
            request.CategoryIds ?? [])
        {
            ETag = etag
        };
        
        return bus.InvokeAsync<IResult>(command);
    }
}
```

**Endpoint Responsibilities**:
- Extract data from HTTP request
- Create command object
- Invoke command via `IMessageBus`
- Return result to client

## Testing Handlers

Handlers are pure functions - easy to test!

```csharp
using NSubstitute;
using Xunit;

public class BookHandlerTests
{
    [Fact]
    public void CreateBookHandler_ShouldStartStreamWithBookAddedEvent()
    {
        // Arrange
        var command = new CreateBook(
            "Clean Code",
            "978-0132350884",
            "A Handbook of Agile Software Craftsmanship",
            new DateOnly(2008, 8, 1),
            Guid.CreateVersion7(),
            [Guid.CreateVersion7()],
            [Guid.CreateVersion7()]);
        
        var session = Substitute.For<IDocumentSession>();
        session.CorrelationId.Returns("test-correlation-id");
        
        // Act
        var result = BookHandlers.Handle(command, session);
        
        // Assert
        Assert.NotNull(result);
        session.Events.Received(1).StartStream<BookAggregate>(
            command.Id,
            Arg.Is<Events.BookAdded>(e => 
                e.Title == "Clean Code" && 
                e.Isbn == "978-0132350884"));
    }
    
    [Fact]
    public async Task UpdateBookHandler_WithWrongETag_ShouldReturn412()
    {
        // Arrange
        var bookId = Guid.CreateVersion7();
        var command = new UpdateBook(bookId, "Updated Title", ...) 
        { 
            ETag = "\"999\"" 
        };
        
        var session = Substitute.For<IDocumentSession>();
        var context = new DefaultHttpContext();
        context.Request.Headers["If-Match"] = "\"999\"";
        
        var streamState = new Marten.Events.StreamState(bookId, 5);
        session.Events.FetchStreamStateAsync(bookId)
            .Returns(Task.FromResult<Marten.Events.StreamState?>(streamState));
        
        // Act
        var result = await BookHandlers.Handle(command, session, context);
        
        // Assert
        var problemResult = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(412, problemResult.StatusCode);
    }
}
```

**Testing Benefits**:
- No HTTP context mocking (unless needed for ETag)
- Pure function testing
- Easy to test edge cases
- Fast test execution
- Clear arrange/act/assert

## Configuration

### Program.cs Setup

```csharp
using Wolverine;
using Wolverine.Marten;

// Configure Marten first
builder.Services.AddMarten(options =>
{
    options.Connection(connectionString);
    // ... Marten configuration
}).IntegrateWithWolverine(); // Important: Integrate with Wolverine

// Add Wolverine
builder.Services.AddWolverine(opts =>
{
    // Auto-discover handlers in this assembly
    opts.Discovery.IncludeAssembly(typeof(Program).Assembly);
    
    // Policies for automatic behavior
    opts.Policies.AutoApplyTransactions();
});
```

### Key Configuration Points

1. **`IntegrateWithWolverine()`**: Connects Marten to Wolverine
2. **`IncludeAssembly()`**: Tells Wolverine where to find handlers
3. **`AutoApplyTransactions()`**: Enables automatic transaction management

## Advanced Patterns

### Cascading Messages

Return multiple outputs from a handler:

```csharp
public static (IResult, SendEmail, UpdateSearchIndex) Handle(
    CreateBook command,
    IDocumentSession session)
{
    var @event = BookAggregate.Create(...);
    session.Events.StartStream(command.Id, @event);
    
    // Return multiple outputs - Wolverine handles them
    return (
        Results.Created(...),
        new SendEmail("admin@library.com", "New book added"),
        new UpdateSearchIndex(command.Id)
    );
}
```

### Side Effects

Publish events or messages after successful commit:

```csharp
public static (IResult, BookCreatedEvent) Handle(
    CreateBook command,
    IDocumentSession session)
{
    var @event = BookAggregate.Create(...);
    session.Events.StartStream(command.Id, @event);
    
    // This event is published after transaction commits
    return (
        Results.Created(...),
        new BookCreatedEvent(command.Id, command.Title)
    );
}
```

## Migration Checklist

When migrating an endpoint to Wolverine:

- [ ] Create command record in `Commands/` folder
- [ ] Create handler in `Handlers/` folder
- [ ] Update endpoint to use `IMessageBus.InvokeAsync()`
- [ ] Remove manual `SaveChangesAsync()` calls
- [ ] Write unit tests for handler
- [ ] Test endpoint with Scalar UI
- [ ] Verify events in database

## Common Patterns

### Pattern 1: Simple Create

```csharp
// Command
public record CreateEntity(string Name, string Description);

// Handler
public static IResult Handle(CreateEntity command, IDocumentSession session)
{
    var @event = EntityAggregate.Create(command.Name, command.Description);
    session.Events.StartStream(Guid.CreateVersion7(), @event);
    return Results.Created(...);
}

// Endpoint
private static Task<IResult> Create(CreateEntityRequest request, IMessageBus bus)
    => bus.InvokeAsync<IResult>(new CreateEntity(request.Name, request.Description));
```

### Pattern 2: Update with ETag

```csharp
// Command
public record UpdateEntity(Guid Id, string Name) 
{ 
    public string? ETag { get; init; } 
}

// Handler
public static async Task<IResult> Handle(
    UpdateEntity command,
    IDocumentSession session,
    HttpContext context)
{
    // ETag validation
    var streamState = await session.Events.FetchStreamStateAsync(command.Id);
    if (streamState == null) return Results.NotFound();
    
    var currentETag = ETagHelper.GenerateETag(streamState.Version);
    if (!ETagHelper.CheckIfMatch(context, currentETag))
        return ETagHelper.PreconditionFailed();
    
    // Business logic
    var aggregate = await session.Events.AggregateStreamAsync<EntityAggregate>(command.Id);
    var @event = aggregate.Update(command.Name);
    session.Events.Append(command.Id, @event);
    
    // Return new ETag
    var newStreamState = await session.Events.FetchStreamStateAsync(command.Id);
    ETagHelper.AddETagHeader(context, ETagHelper.GenerateETag(newStreamState!.Version));
    
    return Results.NoContent();
}
```

### Pattern 3: Delete/Restore

```csharp
// Command
public record SoftDeleteEntity(Guid Id) 
{ 
    public string? ETag { get; init; } 
}

// Handler (similar to Update pattern)
public static async Task<IResult> Handle(
    SoftDeleteEntity command,
    IDocumentSession session,
    HttpContext context)
{
    // ETag validation...
    var aggregate = await session.Events.AggregateStreamAsync<EntityAggregate>(command.Id);
    var @event = aggregate.SoftDelete();
    session.Events.Append(command.Id, @event);
    return Results.NoContent();
}
```

## Troubleshooting

### Handler Not Found

**Problem**: `InvalidOperationException: No handler for command`

**Solution**:
- Ensure handler method is named `Handle`
- Verify handler is in assembly specified in `opts.Discovery.IncludeAssembly()`
- Check handler is `public static`

### Transaction Not Committing

**Problem**: Events not saved to database

**Solution**:
- Verify `IntegrateWithWolverine()` is called on Marten configuration
- Ensure `AutoApplyTransactions()` policy is enabled
- Check handler doesn't throw exceptions

### ETag Not Working

**Problem**: ETag validation not working

**Solution**:
- Ensure `HttpContext` is injected into handler
- Verify `If-Match` header is being sent
- Check ETag format (should be quoted: `"5"`)

## Best Practices

1. **Keep Commands Simple**: Just data, no logic
2. **Handlers are Pure**: No side effects except database writes
3. **Test Handlers**: Easy to test, so write comprehensive tests
4. **Thin Endpoints**: Just routing, no business logic
5. **Use ETags**: For optimistic concurrency on updates/deletes
6. **Document Commands**: XML comments help API consumers

## Summary

Wolverine provides:
- ✅ Clean command/handler separation
- ✅ Automatic transaction management
- ✅ Easy testing
- ✅ Foundation for async messaging
- ✅ Less boilerplate code

The pattern is simple:
1. **Command** = User intent (immutable record)
2. **Handler** = Business logic (pure function)
3. **Endpoint** = HTTP routing (thin layer)

## Next Steps

- **[Event Sourcing Guide](event-sourcing-guide.md)** - Event sourcing concepts and patterns
- **[Marten Guide](marten-guide.md)** - Event sourcing, streams, aggregates, and projections
- [Architecture](../architecture.md)** - System design and CQRS patterns
- **[ETag Guide](etag-guide.md)** - Optimistic concurrency implementation
- [Getting Started](../getting-started.md)** - Setup and running the application
