# Marten Advanced Topics

## Multi-Tenancy

### Conjoined Tenancy (Shared Database)

All tenants share the same PostgreSQL database. Data is isolated by a `tenant_id` column on every table.

```csharp
// Configuration
options.Events.TenancyStyle = TenancyStyle.Conjoined;
options.Policies.AllDocumentsAreMultiTenanted();
```

Every query and event append automatically filters/tags with the current tenant ID — no manual filtering needed.

### Per-Tenant Sessions

Sessions must be scoped to a specific tenant. Wolverine creates tenant-appropriate sessions automatically when the tenant is part of the `IMessageContext`. For manual control:

```csharp
// Open a session for a specific tenant
using var session = store.LightweightSession("acme");       // by tenant ID string
// or
using var session = store.LightweightSession(tenantId);

// Query session
using var query = store.QuerySession("acme");
```

### Global (Non-Tenanted) Documents

Some documents should be accessible across all tenants (e.g., tenant registry, global config):

```csharp
[Marten.Schema.DoNotPartition]
public class Tenant
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
}
```

> `[DoNotPartition]` requires Marten 8.5+. Without it, documents get a `tenant_id` column and cannot be queried globally.

### Conjoined vs Separate Databases

| Strategy | Isolation | Complexity | Use Case |
|----------|-----------|------------|----------|
| **Conjoined** | Row-level (`tenant_id`) | Low | Multi-tenant SaaS, shared infra |
| **Separate Schemas** | Schema-level | Medium | Stricter isolation requirements |
| **Separate Databases** | DB-level | High | Compliance, very high isolation |

For most applications, conjoined tenancy is the right choice.

---

## Async Daemon

The async daemon is a background service that applies events to async projections.

### Configuration

```csharp
.AddAsyncDaemon(DaemonMode.Solo)   // Single instance (most common)
// or:
.AddAsyncDaemon(DaemonMode.HotCold) // Active/standby HA setup
```

- **Solo**: One instance runs the daemon. Fine for most deployments.
- **Hot/Cold**: Two instances; one active, one on standby. For HA scenarios.

### Rebuild Projections

```csharp
// Inject IProjectionDaemon for programmatic control
var daemon = await store.BuildProjectionDaemonAsync();

// Rebuild a single projection from scratch
await daemon.RebuildProjectionAsync<MyProjectionBuilder>(CancellationToken.None);

// Rebuild all projections
await store.Advanced.RebuildAllProjectionsAsync();
```

Rebuilds are safe and idempotent. Use them when:
- Adding a new projection to an existing event store
- Fixing a bug in projection logic
- Migrating projection schema

### Daemon Errors

The daemon retries failed event processing with exponential backoff. Dead-letter events (exhausted retries) are stored in `mt_event_progression` for inspection.

---

## Commit Listeners (Cache + Notifications)

React to projection changes (inserts, updates, deletes) with `IDocumentSessionListener` + `IChangeListener`.

### Implementation

```csharp
public class ProjectionCommitListener : IDocumentSessionListener, IChangeListener
{
    readonly HybridCache _cache;
    readonly INotificationService _notifications;

    // Called AFTER a projection commit (read model updated)
    public async Task AfterCommitAsync(
        IDocumentSession _,
        IChangeSet commit,
        CancellationToken token)
    {
        foreach (var doc in commit.Updated.OfType<BookSearchProjection>())
            await HandleBookChanged(doc, token);

        foreach (var doc in commit.Inserted.OfType<BookSearchProjection>())
            await HandleBookChanged(doc, token);

        foreach (var doc in commit.Deleted.OfType<BookSearchProjection>())
            await _cache.RemoveByTagAsync($"book:{doc.Id}", token);
    }

    async Task HandleBookChanged(BookSearchProjection book, CancellationToken token)
    {
        // 1. Invalidate cache
        await _cache.RemoveByTagAsync($"book:{book.Id}", token);
        await _cache.RemoveByTagAsync("books", token);

        // 2. Push real-time SSE notification
        await _notifications.NotifyAsync(new BookUpdatedNotification(book.Id), token);
    }

    // IDocumentSessionListener requires these; implement as no-ops if unused
    public Task BeforeSaveChangesAsync(IDocumentSession session, CancellationToken token) => Task.CompletedTask;
    public Task AfterSaveChangesAsync(IDocumentSession session, CancellationToken token) => Task.CompletedTask;
    public Task BeforeCommitAsync(IDocumentSession session, IChangeSet commit, CancellationToken token) => Task.CompletedTask;
}
```

### Registration

```csharp
builder.Services.AddMarten(...)
    .AddDocumentSessionListener<ProjectionCommitListener>();
```

> Multiple listeners can be registered; they execute in registration order.

### When Listeners Fire (Async Projections)

```
Command → StartStream/Append → Wolverine commits → return 201
                                    ↓ (background)
                              Async Daemon polls
                                    ↓
                          Projection updated in DB
                                    ↓
                      AfterCommitAsync fires on listener
                        → cache invalidated
                        → SSE notification sent
```

---

## Event Metadata

Marten can automatically track correlation and causation IDs on every event:

```csharp
options.Events.MetadataConfig.CorrelationIdEnabled = true;
options.Events.MetadataConfig.CausationIdEnabled = true;
options.Events.MetadataConfig.HeadersEnabled = true;  // JSON headers dict
```

When Wolverine sets the correlation ID on `IMessageContext`, Marten picks it up automatically. Access metadata on raw events:

```csharp
var events = await session.Events.FetchStreamAsync(bookId);
foreach (var e in events)
{
    Console.WriteLine($"CorrelationId: {e.CorrelationId}");
    Console.WriteLine($"CausationId: {e.CausationId}");
}
```

---

## Natural Keys

Marten supports string-based natural keys as document identity (instead of `Guid`):

```csharp
// Configure natural key
options.Schema.For<Country>()
    .Identity(x => x.Code);  // "US", "GB", "PT"

// Use exactly like Guid-based identity
var country = await session.LoadAsync<Country>("US");
session.Store(new Country { Code = "DE", Name = "Germany" });
```

Natural keys work well for configuration documents, reference data, and any entity with a well-known string identifier.

---

## Performance

### Lightweight Sessions (Important)

Always configure `UseLightweightSessions()` unless you specifically need identity-map caching:

```csharp
builder.Services.AddMarten(...)
    .UseLightweightSessions();
```

Lightweight sessions skip the identity map, reducing memory allocation per request. In an event-sourced system, you rarely need identity-map caching since you load aggregates from events.

### Version 7 GUIDs

All entity IDs must use `Guid.CreateVersion7()`. It creates time-ordered GUIDs that:
- Minimize B-tree page splits (10-30% faster inserts at scale)
- Enable temporal range queries using `CompareTo`
- Follow RFC 9562

### Batch Loading

Use `LoadManyAsync` for batch document loads instead of multiple individual loads:

```csharp
// ✅ Single query for all IDs
var authors = await session.LoadManyAsync<AuthorProjection>(authorId1, authorId2, authorId3);

// ❌ Three separate queries
var a1 = await session.LoadAsync<AuthorProjection>(authorId1);
var a2 = await session.LoadAsync<AuthorProjection>(authorId2);
var a3 = await session.LoadAsync<AuthorProjection>(authorId3);
```

### PostgreSQL Extensions

For full-text search with multilingual support:

```csharp
// Configure unaccent support (removes accent marks for search)
options.UseNGramSearchWithUnaccent();
```

Requires `pg_trgm` and `unaccent` PostgreSQL extensions to be installed.
