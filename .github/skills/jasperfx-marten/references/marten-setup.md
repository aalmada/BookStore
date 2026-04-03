# Marten Setup

## NuGet Packages

```xml
<PackageReference Include="Marten" />
<PackageReference Include="Weasel.Postgresql" />
<!-- For Wolverine integration: -->
<PackageReference Include="WolverineFx.Marten" />
```

## AddMarten() Configuration

```csharp
builder.Services.AddMarten(sp =>
{
    var options = new StoreOptions();
    options.Connection(configuration.GetConnectionString("bookstore")!);

    // Metadata tracking (recommended)
    options.Events.MetadataConfig.CorrelationIdEnabled = true;
    options.Events.MetadataConfig.CausationIdEnabled = true;
    options.Events.MetadataConfig.HeadersEnabled = true;

    // Multi-tenancy (conjoined = shared DB, data isolated by tenant_id)
    options.Events.TenancyStyle = TenancyStyle.Conjoined;
    options.Policies.AllDocumentsAreMultiTenanted();

    // Register all event types explicitly
    options.Events.AddEventType<BookAdded>();
    options.Events.AddEventType<BookUpdated>();
    options.Events.AddEventType<BookSoftDeleted>();

    // Projections
    options.Projections.Snapshot<CategoryProjection>(SnapshotLifecycle.Async);
    options.Projections.Add<BookSearchProjectionBuilder>(ProjectionLifecycle.Async);

    return options;
})
.UseLightweightSessions()           // Better performance (no identity map)
.AddAsyncDaemon(DaemonMode.Solo)    // Background projection daemon
.IntegrateWithWolverine();          // Auto-commit Marten in Wolverine handlers
```

> Always call `UseLightweightSessions()` unless you specifically need identity-map caching.
> `IntegrateWithWolverine()` makes Wolverine automatically commit Marten sessions — no manual `SaveChangesAsync()`.

## Schema Initialization

Apply the Marten schema to PostgreSQL on startup:

```csharp
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();
    await store.Storage.ApplyAllConfiguredChangesToDatabaseAsync();
}
```

> This is intentionally blocking — the schema must exist before any requests are handled.

## Session Types

| Type | Purpose | When to Use |
|------|---------|-------------|
| `IDocumentSession` | Read + write, tracks changes | Command handlers, mutation endpoints |
| `IQuerySession` | Read-only, lighter weight | Query handlers, GET endpoints |

Both are injected via DI and scoped per request/handler.

```csharp
// Write handler (Wolverine injects IDocumentSession)
public static IResult Handle(CreateBook command, IDocumentSession session)

// Read handler
public static async Task<IResult> Handle(GetBooks query, IQuerySession session)
```

> `IQuerySession` is preferred for reads — it has a smaller footprint and makes intent clear.

## Wolverine Integration

With `.IntegrateWithWolverine()`:
- Marten session is automatically enrolled in Wolverine's transaction
- `SaveChangesAsync()` is called automatically at the end of the handler
- The Wolverine outbox is stored in the same PostgreSQL database
- Transactional consistency is maintained between event store and outbox

```csharp
// ✅ Correct: Let Wolverine commit
public static IResult Handle(CreateBook command, IDocumentSession session)
{
    session.Events.StartStream<BookAggregate>(command.Id, @event);
    return Results.Created(...);
}

// ❌ Wrong: Manual save breaks transactional guarantees
public static async Task<IResult> Handle(CreateBook command, IDocumentSession session)
{
    session.Events.StartStream<BookAggregate>(command.Id, @event);
    await session.SaveChangesAsync(); // Don't do this with Wolverine
    return Results.Created(...);
}
```

## Document Indexes

Configure indexes in `AddMarten()` for fast queries:

```csharp
options.Schema.For<ApplicationUser>()
    .UniqueIndex(UniqueIndexType.Computed, x => x.NormalizedEmail!)
    .UniqueIndex(UniqueIndexType.Computed, x => x.NormalizedUserName!)
    .Index(x => x.NormalizedEmail)
    .GinIndexJsonData();   // Full-text GIN index on the entire JSON document
```
