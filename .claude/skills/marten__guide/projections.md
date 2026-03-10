# Projections

## Quick Decision Guide

| Scenario | Approach | Base class / Registration |
|----------|----------|--------------------------|
| One aggregate stream → one read model (entity detail view) | [POCO snapshot projection](#poco-snapshot-projection) | No base class; `Projections.Snapshot<T>()` |
| One aggregate stream → complex read model needing full control | [SingleStreamProjection](#singlestreamprojection) | `SingleStreamProjection<T, TId>`; `Projections.Add<T>()` |
| Multiple streams → one view (dashboard, rollup, summary) | [MultiStreamProjection](#multi-stream-projection) | `MultiStreamProjection<T, TId>`; `Projections.Add<T>()` |
| Chain/stage projections for throughput (composite) | [Composite projection](#composite-projection) | See composite section |
| One event → one document (audit log, history, reporting side table) | [EventProjection](#event-projection) | `EventProjection`; `Projections.Add<T>()` |
| Load reference data (documents) into a projection without N+1 queries | [Event Enrichment](#event-enrichment) | `EnrichEventsAsync` override |

Ensure the aggregate and its events exist before creating projections. See [`aggregate.md`](aggregate.md).

---

## POCO Snapshot Projection

The simplest and most common approach. The projection class is a plain C# class (no base class required). Marten matches `Create` / `Apply` static or instance methods by convention.

1. **Define the Projection Class**
   - Create a `class` in `src/BookStore.ApiService/Projections/`
   - **Naming**: `{Resource}Projection` (e.g., `AuthorProjection`)
   - No base class — Marten discovers it via method conventions
   - Use **`IEvent<T>`** wrappers in `Apply` methods to get event metadata (`Timestamp`, `Version`)
   - **Template**: `templates/Projection.cs`

2. **Configure in Marten**
   - Open `src/BookStore.ApiService/Infrastructure/Extensions/MartenConfigurationExtensions.cs`
   - Register in `RegisterProjections` using `Snapshot`:
     ```csharp
     _ = options.Projections.Snapshot<AuthorProjection>(SnapshotLifecycle.Async);
     ```
   - Use `SnapshotLifecycle.Async` (background) unless you need immediate consistency (`Inline`)

3. **Indexing (Optional)**
   - In `ConfigureIndexes`:
     ```csharp
     _ = options.Schema.For<AuthorProjection>()
         .Index(x => x.Name)
         .NgramIndex(x => x.Name)
         .Index(x => x.Deleted);
     ```

4. **Querying**
   - Query by ID: `session.LoadAsync<AuthorProjection>(streamId)`
   - Query collection: `session.Query<AuthorProjection>().Where(...)`

**Related**: Use [`queries.md`](queries.md) to add GET endpoints that expose this projection.

---

## SingleStreamProjection

Use when you need explicit control over lifecycle (soft-delete, re-creation, `IncludeType` allowlist). Inherits `SingleStreamProjection<T, TId>`.

1. **Define the Projection Class**
   - Create a `class` in `src/BookStore.ApiService/Projections/`
   - **Base Class**: `SingleStreamProjection<{Resource}Projection, Guid>`
   - Enable `Options.CacheLimitPerTenant` for performance
   - **Template**: `templates/Projection.cs` (see SingleStreamProjection variant)

2. **Configure in Marten**
   - Register in `RegisterProjections` using `Add`:
     ```csharp
     options.Projections.Add<{Resource}Projection>(ProjectionLifecycle.Async);
     ```

3. **Soft-delete support**
   - Call `DeleteEvent<{Resource}SoftDeleted>()` in the constructor to hard-delete the document when this event arrives, **or** manually set `Deleted = true` in `Apply` if you want soft-delete in the read model

4. **`Evolve` method (alternative to `Apply`)**
   - For finer control, override `Evolve` instead of individual `Apply` methods. It receives the current snapshot, stream id, and the raw `IEvent`, and must return the updated snapshot:
     ```csharp
     public override ProviderShift Evolve(ProviderShift snapshot, Guid id, IEvent e)
     {
         switch (e.Data)
         {
             case ProviderJoined joined:
                 snapshot = new ProviderShift(joined.BoardId);
                 break;
             case ProviderReady:
                 snapshot.Status = ProviderStatus.Ready;
                 break;
         }
         return snapshot;
     }
     ```
   - Useful when you need access to event metadata (`e.Timestamp`, `e.Version`) or when handling `Updated<T>` / `References<T>` synthetic events from a composite stage

---

## Multi-stream projection

Follow this guide to create a **Multi-Stream Projection** in Marten. This allows you to aggregate data across many different streams into a single document (or multiple documents based on grouping).

1. **Define the Projection Class**
   - Create a `class` in `src/BookStore.ApiService/Projections/`
   - **Naming**: `{Summary}ProjectionBuilder` for the projection, `{Summary}` for the view document
   - **Base Class**: `MultiStreamProjection<{Summary}, Guid>`
   - Enable `Options.CacheLimitPerTenant = 1000` in the constructor for performance
   - Use `Identity<TEvent>(e => e.AggregateId)` for simple routing, `CustomGrouping(new MyGrouper())` for complex routing
   - **Template**: `templates/MultiStreamProjection.cs`

2. **Configure in Marten**
   - Open `src/BookStore.ApiService/Infrastructure/Extensions/MartenConfigurationExtensions.cs`
   - Register:
     ```csharp
     options.Projections.Add<{Summary}ProjectionBuilder>(ProjectionLifecycle.Async);
     ```

3. **Custom Grouper (complex routing)**
   - When event routing can't be expressed with `Identity<T>`, implement `IAggregateGrouper<TId>`:
     ```csharp
     public class MyGrouper : IAggregateGrouper<Guid>
     {
         public async Task Group(IQuerySession session, IEnumerable<IEvent> events, IEventGrouping<Guid> grouping)
         {
             // Batch-load docs to resolve IDs, then call grouping.AddEvents(...)
         }
     }
     ```

4. **`DetermineAction` method (alternative to `Apply`)**
   - For full control over whether to store or delete, override `DetermineAction` instead of `Apply` methods. Returns a `(snapshot, ActionType)` tuple:
     ```csharp
     public override (BoardSummary, ActionType) DetermineAction(
         BoardSummary snapshot, Guid identity, IReadOnlyList<IEvent> events)
     {
         snapshot ??= new BoardSummary { Id = identity };

         if (events.TryFindReference<Board>(out var board))
             snapshot.Board = board;

         foreach (var shift in events.AllReferenced<ProviderShift>())
             snapshot.ActiveProviders[shift.ProviderId] = shift;

         return (snapshot, ActionType.Store);
     }
     ```
   - Use `events.AllReferenced<T>()` and `events.TryFindReference<T>(out var entity)` to access `References<T>` enriched data (see [Event Enrichment](#event-enrichment))

5. **Indexing**
   - Add indexes in `ConfigureIndexes` if you need filtering/sorting by non-ID fields.

---

## Event Enrichment

> **Source:** Jeremy Miller (JasperFx), [Easier Query Models with Marten](https://jeremydmiller.com/2026/01/20/easier-query-models-with-marten/), January 20, 2026

Use **Event Enrichment** when a projection needs to incorporate reference data from the document store (e.g., a `Provider` document stored alongside an event stream). Calling `LoadAsync<T>()` inside each `Apply` / `Create` method triggers an N+1 — one database round trip per event. Enrichment batches all lookups into a single round trip per batch.

Override `EnrichEventsAsync` on a `SingleStreamProjection<T>` or `MultiStreamProjection<T>`:

```csharp
public override async Task EnrichEventsAsync(
    SliceGroup<ProviderShift, Guid> group,
    IQuerySession querySession,
    CancellationToken cancellation)
{
    await group
        // Which document type to look up
        .EnrichWith<Provider>()
        // Which event type references that document
        .ForEvent<ProviderJoined>()
        // How to get the document ID from the event
        .ForEntityId(x => x.ProviderId)
        // Option A — replace the original event with an enriched version
        .EnrichAsync((slice, e, provider) =>
        {
            slice.ReplaceEvent(e, new EnhancedProviderJoined(e.Data.BoardId, provider));
        });
        // Option B — wrap as a References<T> synthetic event (use with DetermineAction/AllReferenced<T>)
        // .AddReferences();
}
```

**Option A — `ReplaceEvent`**: Swap the original event with a richer type (e.g., `EnhancedProviderJoined`) that carries the loaded entity. Handle it in `Evolve`:

```csharp
public override ProviderShift Evolve(ProviderShift snapshot, Guid id, IEvent e)
{
    switch (e.Data)
    {
        case EnhancedProviderJoined joined:
            snapshot = new ProviderShift(joined.BoardId, joined.Provider);
            break;
    }
    return snapshot;
}
```

**Option B — `AddReferences`**: Marten wraps each loaded entity as a `References<T>` synthetic event. Consume it in `DetermineAction` or `Evolve` via `AllReferenced<T>()` / `TryFindReference<T>()`:

```csharp
if (events.TryFindReference<Board>(out var board))
    snapshot.Board = board;

foreach (var shift in events.AllReferenced<ProviderShift>())
    snapshot.ActiveProviders[shift.ProviderId] = shift;
```

See [Marten docs — Event Enrichment](https://martendb.io/events/projections/enrichment.html) for additional patterns.

---

## Composite projection

Use **Composite Projections** to stage dependent projections. Stage 1 projections run first; Stage 2 projections can rely on their output. The entire composite reads the event range **once** and writes all projection updates in a single batch.

> **Source:** Jeremy Miller (JasperFx), [Easier Query Models with Marten](https://jeremydmiller.com/2026/01/20/easier-query-models-with-marten/), January 20, 2026

1. **Define projections** for each stage as normal `SingleStreamProjection`, `MultiStreamProjection`, or POCO snapshot classes.

2. **Configure in Marten**
   - Register as a named composite group:
     ```csharp
     opts.Projections.CompositeProjectionFor("TeleHealth", projection =>
     {
         // Stage 1: run in parallel
         projection.Add<ProviderShiftProjection>();
         projection.Add<AppointmentProjection>();
         projection.Snapshot<Board>();

         // Stage 2: runs only after Stage 1 commits
         projection.Add<AppointmentDetailsProjection>(2);
         projection.Add<BoardSummaryProjection>(2);
     });
     ```
   - The integer argument is the stage number — default is `1`.

3. **`Updated<T>` synthetic events**
   - After Stage 1 commits, Marten publishes `Updated<T>` events to Stage 2 projections for every document changed. Stage 2 projections receive the exact up-to-date snapshot without making any additional database round trips.
   - Use `Identity<Updated<T>>(x => x.Entity.SomeId)` in the Stage 2 projection constructor to route these events:
     ```csharp
     Identity<Updated<Appointment>>(x => x.Entity.BoardId ?? Guid.Empty);
     Identity<Updated<Board>>(x => x.Entity.Id);
     Identity<Updated<ProviderShift>>(x => x.Entity.BoardId);
     ```
   - Access the upstream snapshot in `Evolve` / `DetermineAction` via `e.Data` as `Updated<T>`:
     ```csharp
     case Updated<Appointment> updated:
         snapshot.Status = updated.Entity.Status;
         break;
     ```

4. **`References<T>` and `AllReferenced<T>()` (from enrichment)**
   - Stage 2 projections can also call `EnrichEventsAsync` to pull in reference documents. Use `.AddReferences()` (instead of `.EnrichAsync(...)`) to wrap the loaded entity as a `References<T>` synthetic event:
     ```csharp
     await group.EnrichWith<Board>()
         .ForEvent<AppointmentRouted>()
         .ForEntityId(x => x.BoardId)
         .AddReferences();
     ```
   - In `DetermineAction` or `Evolve`, consume the reference via:
     ```csharp
     // Single reference
     if (events.TryFindReference<Board>(out var board))
         snapshot.Board = board;

     // Multiple references
     foreach (var shift in events.AllReferenced<ProviderShift>())
         snapshot.ActiveProviders[shift.ProviderId] = shift;
     ```
   - Alternatively use `group.ReferencePeerView<T>()` in `EnrichEventsAsync` to automatically pull in the peer view document from another projection in the same composite group:
     ```csharp
     public override Task EnrichEventsAsync(...) =>
         group.ReferencePeerView<Board>();
     ```

5. **Notes**
   - Only use composite projections when a projection genuinely depends on another projection's output being committed first
   - Otherwise prefer independent `Projections.Add<T>(ProjectionLifecycle.Async)` registrations
   - See [Marten docs — Composite Projections](https://martendb.io/events/projections/composite.html) for rebuild, versioning, and non-stale query behaviour

---

## Event projection

Follow this guide to create an **Event Projection** in Marten. Best for flattening events into queryable documents (1 event → 1 document) or writing to side tables.

1.  **Define the Projection Class**
    -   Create a `class` in `src/BookStore.ApiService/Projections/`
    -   **Base Class**: `EventProjection`
    -   Use convention methods: `Create(IEvent<T>)` returns the new document; `Project(T, IDocumentOperations)` performs arbitrary operations
    -   **Template**: `templates/EventProjection.cs`

2.  **Configure in Marten**
    -   Open `src/BookStore.ApiService/Infrastructure/Extensions/MartenConfigurationExtensions.cs`
    -   Register:
        ```csharp
        options.Projections.Add<AuditLogProjection>(ProjectionLifecycle.Async);
        ```

3.  **Use Cases**
    -   History tables
    -   Audit logs
    -   Flattening stream data for reporting/analytics (without aggregation)
