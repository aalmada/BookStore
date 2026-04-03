# Marten Projections

Projections transform events into read models (documents) optimized for queries. The async daemon processes events in the background and keeps projections up to date.

## Projection Types

| Type | Events from | Registration | Use Case |
|------|-------------|-------------|----------|
| `SingleStreamProjection<T>` | 1 stream → 1 document | `Snapshot<T>()` | Author, Category, Publisher details |
| `MultiStreamProjection<T, TId>` | N streams → 1 document | `Add<TProjection>()` | Denormalized views, statistics |

## SingleStreamProjection

One event stream maps to one document. The stream ID becomes the document ID.

### Simple Pattern (Class-based with methods)

```csharp
public class AuthorProjection
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Biography { get; set; }
}

public class AuthorProjectionBuilder : SingleStreamProjection<AuthorProjection>
{
    // Called when first event in the stream arrives
    public AuthorProjection Create(AuthorAdded @event) =>
        new()
        {
            Id = @event.Id,
            Name = @event.Name,
            Biography = @event.Biography,
        };

    // Called for subsequent events
    public void Apply(AuthorUpdated @event, AuthorProjection projection)
    {
        projection.Name = @event.Name;
        projection.Biography = @event.Biography;
    }

    // Returning a new instance replaces the existing projection
    public AuthorProjection Apply(AuthorRenamed @event, AuthorProjection current) =>
        current with { Name = @event.NewName };
}
```

### Self-Aggregate Pattern (projection is its own builder)

When the projection document contains all the Apply logic directly:

```csharp
public class CategoryProjection
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Deleted { get; set; }

    // Static factory if projection contains no prior state
    public static CategoryProjection Create(CategoryAdded @event) =>
        new() { Id = @event.Id, Name = @event.Name };

    // Instance Apply mutates the projection
    public void Apply(CategoryUpdated @event) => Name = @event.Name;
    public void Apply(CategoryDeleted _) => Deleted = true;
}
```

Register:
```csharp
// Use Snapshot for SingleStreamProjection
options.Projections.Snapshot<CategoryProjection>(SnapshotLifecycle.Async);
// OR if you have a separate builder class:
options.Projections.Add<AuthorProjectionBuilder>(ProjectionLifecycle.Async);
```

> `Snapshot<T>` is shorthand: it expects the document itself to have `Evolve`/`Create`/`Apply` methods or it will use a discovered builder. Prefer `Add<TBuilder>()` when you have a separate builder class.

## MultiStreamProjection

Events from **many streams** collapse into one document. You control routing via `Identity<TEvent>()`.

```csharp
public class BookStatisticsProjection : MultiStreamProjection<BookStatistics, Guid>
{
    public BookStatisticsProjection()
    {
        // Route BookAddedToFavorites events from any User stream to the Book document
        Identity<BookAddedToFavorites>(e => e.BookId);
        Identity<BookRemovedFromFavorites>(e => e.BookId);
        Identity<BookPurchased>(e => e.BookId);
    }

    public static BookStatistics Create(BookAdded @event) =>
        new() { Id = @event.Id, Title = @event.Title };

    public void Apply(BookAddedToFavorites _, BookStatistics projection) =>
        projection.FavoriteCount++;

    public void Apply(BookRemovedFromFavorites _, BookStatistics projection) =>
        projection.FavoriteCount = Math.Max(0, projection.FavoriteCount - 1);

    public void Apply(BookPurchased _, BookStatistics projection) =>
        projection.PurchaseCount++;
}
```

Register:
```csharp
options.Projections.Add<BookStatisticsProjection>(ProjectionLifecycle.Async);
```

> Why MultiStream? "Like" events live in User streams. A single-stream Book projection never sees those events. MultiStreamProjection fans events from thousands of User streams into one BookStatistics document.

## Projection Lifecycle

| Lifecycle | Behaviour | Use Case |
|-----------|-----------|----------|
| `Inline` | Updated synchronously before the write completes | Strong consistency required |
| `Async` | Updated by background daemon asynchronously | **Preferred** — better write throughput |
| `Live` | Never stored, always rebuilt on demand | Ad-hoc queries, debugging |

```csharp
options.Projections.Snapshot<CategoryProjection>(SnapshotLifecycle.Async);
options.Projections.Add<BookSearchProjectionBuilder>(ProjectionLifecycle.Async);
```

## Event Enrichment (Avoiding N+1 Queries)

When a projection needs data from another document (e.g., publisher name when processing `BookAdded`), naive loading inside `Apply` causes N+1 queries. Use the `EnrichEventsAsync` hook to batch-load reference data.

```csharp
public class BookSearchProjectionBuilder : MultiStreamProjection<BookSearchProjection, Guid>
{
    public BookSearchProjectionBuilder()
    {
        Identity<BookAdded>(x => x.Id);
        Identity<BookUpdated>(x => x.Id);
    }

    // Batch-load reference data BEFORE Evolve is called
    public override async Task EnrichEventsAsync(
        SliceGroup<BookSearchProjection, Guid> group,
        IQuerySession querySession,
        CancellationToken cancellation)
    {
        // Load publishers in bulk, attach as References<PublisherProjection> events
        await group
            .EnrichWith<PublisherProjection>()
            .ForEvent<BookAdded>()
            .ForEntityId(x => x.PublisherId)
            .AddReferences();

        // Collections are supported too
        await group
            .EnrichWith<AuthorProjection>()
            .ForEvent<BookAdded>()
            .ForEntityId(x => x.AuthorIds)   // x.AuthorIds is List<Guid>
            .AddReferences();
    }

    public override BookSearchProjection Evolve(
        BookSearchProjection snapshot,
        Guid id,
        IEvent e)
    {
        snapshot ??= new BookSearchProjection { Id = id };

        switch (e.Data)
        {
            case BookAdded added:
                snapshot.Title = added.Title;
                snapshot.Isbn = added.Isbn;
                break;

            case References<PublisherProjection> publisher:
                snapshot.PublisherName = publisher.Entity.Name;
                break;

            case References<AuthorProjection> authors:
                snapshot.AuthorNames = string.Join(", ", authors.Entities.Select(a => a.Name));
                break;
        }

        return snapshot;
    }
}
```

> The **Fetch → Slice → Enrich → Evolve → Commit** pipeline is designed to minimize database round trips. Push IO to Enrichment, keep `Evolve` pure and side-effect free.

## Composite Projections

Run multiple projections in dependency order (downstream projections see upstream data).

```csharp
opts.Projections.CompositeProjectionFor("TeleHealth", projection =>
{
    // Stage 1: Base projections
    projection.Add<ProviderShiftProjection>();
    projection.Snapshot<Board>();

    // Stage 2: Uses Stage 1 results
    projection.Add<BoardSummaryProjection>(stage: 2);
});
```

Stage 2 projections receive synthetic `Updated<T>` events with the latest upstream snapshot:

```csharp
public class BoardSummaryProjection : MultiStreamProjection<BoardSummary, Guid>
{
    public BoardSummaryProjection()
    {
        Identity<Updated<Board>>(x => x.Entity.Id);
    }

    public override BoardSummary Evolve(BoardSummary snapshot, Guid id, IEvent e)
    {
        if (e.Data is Updated<Board> updated)
            snapshot.BoardName = updated.Entity.Name;

        return snapshot;
    }
}
```

## Querying Projections

Projections are just documents — query them the same way:

```csharp
// Basic query
var books = await session.Query<BookSearchProjection>()
    .Where(b => !b.Deleted)
    .OrderBy(b => b.Title)
    .ToListAsync();

// Full-text search on computed field
var results = await session.Query<BookSearchProjection>()
    .Where(b => b.SearchText.PlainTextSearch("clean code"))
    .ToListAsync();

// Load single projection by ID
var book = await session.LoadAsync<BookSearchProjection>(bookId);
```

## Rebuilding Projections

Force a full rebuild from all historical events:

```csharp
// Rebuild a single projection
await daemon.RebuildProjectionAsync<BookSearchProjectionBuilder>(CancellationToken.None);

// Rebuild all projections
await store.Advanced.RebuildAllProjectionsAsync();
```

> Rebuilding is safe — it's how you add a new projection to an existing event store. All historical events are replayed.

## Side Effects in Projections

Emit messages (e.g., Wolverine messages) from inside a projection after a successful commit:

```csharp
public override void RaiseSideEffects(IDocumentOperations ops, IEventSlice<MyProjection> slice)
{
    foreach (var evt in slice.Events())
    {
        if (evt.Data is GoalReached goal)
            slice.PublishMessage(new SendAchievementEmail(goal.UserId));
    }
}
```

> `PublishMessage` uses Wolverine's transactional outbox — the message is only sent after the commit succeeds.
