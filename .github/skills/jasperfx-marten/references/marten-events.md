# Marten Event Sourcing

## Event Design

Events are **immutable records** that represent facts that have already occurred.

```csharp
// ✅ Correct: past tense, record, immutable
public record BookAdded(
    Guid Id,
    string Title,
    string? Isbn,
    DateOnly? PublicationDate,
    Guid? PublisherId,
    List<Guid> AuthorIds);

// ❌ Wrong: imperative name
public record AddBook(Guid Id, string Title);
```

**Rules:**
- `record` types — immutable by design
- Past-tense names: `BookAdded`, `OrderShipped`, `UserRegistered`
- Self-contained — include all data needed to apply the change
- Register every event type with `options.Events.AddEventType<T>()`

## Stream Operations

### Start a New Stream

```csharp
// Start stream with one event
session.Events.StartStream<TAggregate>(streamId, @event);

// Start stream with multiple events
session.Events.StartStream<TAggregate>(streamId, event1, event2, event3);
```

`StartStream` creates a new event stream. The `TAggregate` type parameter is stored as stream metadata.

### Append to Existing Stream

```csharp
// Append one event
session.Events.Append(streamId, @event);

// Append multiple events atomically
session.Events.Append(streamId, event1, event2);

// Append with expected version (optimistic concurrency)
session.Events.Append(streamId, expectedVersion, @event);
```

> When using Wolverine, these are committed automatically at the end of the handler.

### Load Aggregate State

Replay all events in a stream to reconstruct the current aggregate state:

```csharp
var aggregate = await session.Events
    .AggregateStreamAsync<BookAggregate>(bookId);

if (aggregate is null)
    return Results.NotFound();

// Now use aggregate to validate business rules and produce new events
var @event = aggregate.UpdateTitle(command.Title);
session.Events.Append(bookId, @event);
```

> `AggregateStreamAsync` is the primary way to load aggregate state — it replays events rather than loading a snapshot.

### Get Stream State (Version / ETag)

Get lightweight stream metadata without loading the aggregate:

```csharp
var state = await session.Events.FetchStreamStateAsync(bookId);

if (state is null)
    return Results.NotFound();

// Use version as ETag for optimistic concurrency
var etag = $"\"{state.Version}\"";

// Available metadata:
// state.Id         — stream ID (Guid)
// state.Version    — current event count
// state.AggregateTypeName — type name registered at StartStream
// state.Created    — when stream was created
// state.LastTimestamp — timestamp of last event
// state.IsArchived — whether the stream is archived
```

### Fetch All Events in a Stream

```csharp
// Get typed events with metadata
var events = await session.Events.FetchStreamAsync(bookId);

foreach (var evt in events)
{
    Console.WriteLine($"Version {evt.Version}: {evt.EventTypeName}");
    if (evt.Data is BookAdded added)
        Console.WriteLine($"  Title: {added.Title}");
}
```

### Query Events Across Streams

```csharp
// Query raw events with LINQ
var recent = await session.Events.QueryAllRawEvents()
    .Where(e => e.Timestamp > DateTimeOffset.UtcNow.AddHours(-1))
    .OrderByDescending(e => e.Timestamp)
    .Take(100)
    .ToListAsync();

// Filter by event type (string name)
var bookEvents = await session.Events.QueryAllRawEvents()
    .Where(e => e.EventTypeName == "book_added")
    .ToListAsync();
```

## Aggregate Classes

An aggregate rebuilds state by applying events in sequence.

```csharp
public class BookAggregate : ISoftDeleted
{
    // Current state
    public Guid Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public bool Deleted { get; set; }       // ISoftDeleted
    public DateTimeOffset? DeletedAt { get; set; }

    // ─── Apply methods (called by Marten to replay events) ───────────────────

    // Create — can be static or instance
    void Apply(BookAdded @event)
    {
        Id = @event.Id;
        Title = @event.Title;
        Deleted = false;
    }

    void Apply(BookUpdated @event) => Title = @event.Title;

    void Apply(BookSoftDeleted _)
    {
        Deleted = true;
        DeletedAt = DateTimeOffset.UtcNow;
    }

    void Apply(BookRestored _)
    {
        Deleted = false;
        DeletedAt = null;
    }

    // ─── Command methods (business logic → produce events) ───────────────────

    // Static factory for creation (no prior state needed)
    public static BookAdded CreateEvent(Guid id, string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required");
        return new BookAdded(id, title, null, null, null, []);
    }

    // Instance method for updates (validates against current state)
    public BookUpdated UpdateEvent(string title)
    {
        if (Deleted)
            throw new InvalidOperationException("Cannot update a deleted book");
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required");
        return new BookUpdated(Id, title, Isbn, ...);
    }

    public BookSoftDeleted SoftDeleteEvent()
    {
        if (Deleted)
            throw new InvalidOperationException("Already deleted");
        return new BookSoftDeleted(Id, DateTimeOffset.UtcNow);
    }
}
```

### ISoftDeleted

Implementing `ISoftDeleted` tells Marten this aggregate supports soft-deletion:

```csharp
public class MyAggregate : ISoftDeleted
{
    public bool Deleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
```

## Full Handler Example

Combining all the above in a typical Wolverine handler:

```csharp
public static class BookHandlers
{
    // CREATE — starts a new stream
    public static IResult Handle(CreateBook command, IDocumentSession session)
    {
        var @event = BookAggregate.CreateEvent(command.Id, command.Title);
        session.Events.StartStream<BookAggregate>(command.Id, @event);

        return TypedResults.Created(
            $"/api/books/{command.Id}",
            new { id = command.Id });
    }

    // UPDATE — appends to existing stream
    public static async Task<IResult> Handle(
        UpdateBook command,
        IDocumentSession session)
    {
        var aggregate = await session.Events
            .AggregateStreamAsync<BookAggregate>(command.Id);

        if (aggregate is null)
            return TypedResults.NotFound();

        var @event = aggregate.UpdateEvent(command.Title);
        session.Events.Append(command.Id, @event);

        return TypedResults.NoContent();
    }

    // DELETE — soft delete
    public static async Task<IResult> Handle(
        DeleteBook command,
        IDocumentSession session)
    {
        var aggregate = await session.Events
            .AggregateStreamAsync<BookAggregate>(command.Id);

        if (aggregate is null)
            return TypedResults.NotFound();

        var @event = aggregate.SoftDeleteEvent();
        session.Events.Append(command.Id, @event);

        return TypedResults.NoContent();
    }
}
```

## Optimistic Concurrency with Stream Version

Use `FetchStreamStateAsync` to get the current version, return it as an ETag, validate on updates:

```csharp
public static async Task<IResult> Handle(
    UpdateBook command,
    IDocumentSession session,
    HttpContext ctx)
{
    var state = await session.Events.FetchStreamStateAsync(command.Id);
    if (state is null) return TypedResults.NotFound();

    var ifMatch = ctx.Request.Headers.IfMatch.FirstOrDefault();
    var currentETag = $"\"{state.Version}\"";

    if (ifMatch is not null && ifMatch != currentETag)
        return TypedResults.Problem(statusCode: 412, title: "Precondition Failed");

    var aggregate = await session.Events.AggregateStreamAsync<BookAggregate>(command.Id);
    session.Events.Append(command.Id, aggregate!.UpdateEvent(command.Title));

    return TypedResults.NoContent();
}
```

> See the [etag skill](../../etag/SKILL.md) for the full ETag pattern.
