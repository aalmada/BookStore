# Wolverine + Marten Integration

## Setup and Configuration

Call these three extensions in order when wiring up Marten in `Program.cs` or a service extension:

```csharp
services.AddMarten(options => { ... })
    .UseLightweightSessions()                   // avoids identity-map overhead
    .AddAsyncDaemon(DaemonMode.Solo)            // runs async projections in-process
    .PublishEventsToWolverine("marten")         // forward new events to Wolverine handlers
    .IntegrateWithWolverine(x =>
        x.UseWolverineManagedEventSubscriptionDistribution = true);
```

Configure Wolverine itself with transaction and middleware policies:

```csharp
services.AddWolverine(opts =>
{
    opts.Policies.AutoApplyTransactions();                         // commit after every handler
    opts.Policies.AddMiddleware(typeof(WolverineCorrelationMiddleware));
    opts.Policies.AddMiddleware(typeof(WolverineETagMiddleware));
    opts.Durability.EnableInboxPartitioning = true;
    opts.Durability.InboxStaleTime  = TimeSpan.FromMinutes(10);
    opts.Durability.OutboxStaleTime = TimeSpan.FromMinutes(10);
    opts.UnknownMessageBehavior = UnknownMessageBehavior.DeadLetterQueue;
});
```

| Setting | Effect |
|---|---|
| `UseLightweightSessions()` | Wolverine opens a `LightweightDocumentSession` per handler — no identity-map, lower overhead |
| `PublishEventsToWolverine("marten")` | Every event appended to a stream is forwarded to matching `Handle(IEvent<T>)` handlers |
| `IntegrateWithWolverine()` | Shares the same Postgres connection/transaction; enables durable outbox |
| `UseWolverineManagedEventSubscriptionDistribution` | Wolverine divides event subscriptions across instances instead of Marten's default algorithm |
| `AutoApplyTransactions()` | Wolverine wraps each handler in a Marten unit-of-work; commits on success, rolls back on exception |

---

## Session Management and Tenant Scoping

Because `AutoApplyTransactions()` manages the session lifecycle, **never call `SaveChangesAsync()` or `CommitChangesAsync()` in a handler**.

For multi-tenant projects, register `IDocumentSession` as a scoped service that checks `IMessageContext` first, then falls back to the HTTP-layer `ITenantContext`:

```csharp
services.AddScoped<IDocumentSession>(sp =>
{
    var store = sp.GetRequiredService<IDocumentStore>();

    // Inside a Wolverine handler the envelope carries the tenant ID
    var messageContext = sp.GetService<IMessageContext>();
    if (messageContext?.TenantId != null)
        return store.LightweightSession(messageContext.TenantId);

    // HTTP requests use the middleware-resolved tenant
    var tenantContext = sp.GetRequiredService<ITenantContext>();
    return store.LightweightSession(tenantContext.TenantId);
});

// Expose IQuerySession through the same instance
services.AddScoped<IQuerySession>(sp => sp.GetRequiredService<IDocumentSession>());
```

> **Why this matters**: When Marten fires an event subscription and Wolverine dispatches the resulting handler, there is no `HttpContext`. Without this pattern the handler receives a session scoped to the wrong (or default) tenant.

---

## Command Handler Pattern with Marten

Wolverine handlers receive `IDocumentSession` by parameter injection (auto-wired via DI):

```csharp
public static async Task<IResult> Handle(
    CreateBook command,
    IDocumentSession session,   // auto-injected, already tenant-scoped
    HybridCache cache,
    ILogger logger)
{
    var eventResult = BookAggregate.CreateEvent(command.Id, command.Title, ...);
    if (eventResult.IsFailure)
        return eventResult.ToProblemDetails();

    session.Events.StartStream<BookAggregate>(command.Id, eventResult.Value);

    // Invalidate cache inline — no need to await commit, Wolverine does it
    await cache.RemoveByTagAsync([CacheTags.BookList], default);

    return Results.Created($"/api/admin/books/{command.Id}", new { id = command.Id });
}
```

Key rules:
- `session.Events.StartStream<TAggregate>(id, events)` — opens a new stream, also enforces that no stream with `id` exists yet.
- `session.Events.Append(id, events)` — appends to an existing stream.
- `session.Events.AggregateStreamAsync<TAggregate>(id)` — replays events into the aggregate.
- Do **not** call `session.SaveChangesAsync()` — `AutoApplyTransactions()` handles the commit.

---

## Event-Driven Handlers (Reacting to Marten Events)

`PublishEventsToWolverine()` forwards every committed event to any handler whose first parameter is `IEvent<TEventType>`. Use `wrapper.TenantId` to get the originating tenant when scheduling downstream messages.

```csharp
// React to BookSaleScheduled event to schedule future commands
public static async Task Handle(
    JasperFx.Events.IEvent<BookSaleScheduled> wrapper,
    IMessageContext bus)
{
    var tenantId = wrapper.TenantId ?? "";
    var @event = wrapper.Data;

    // Schedule deferred commands at specific DateTimeOffset values
    await bus.ScheduleAsync(
        new ApplyBookDiscount(@event.Id, @event.Sale.Percentage, tenantId),
        @event.Sale.Start);

    await bus.ScheduleAsync(
        new RemoveBookDiscount(@event.Id, tenantId),
        @event.Sale.End);
}
```

`IEvent<T>` exposes:

| Property | Type | Description |
|---|---|---|
| `Data` | `T` | The event payload |
| `TenantId` | `string?` | The tenant that committed the event |
| `StreamId` | `Guid` | The aggregate stream ID |
| `Version` | `long` | The event's position in the stream |
| `Sequence` | `long` | Global sequence number across all streams |
| `Timestamp` | `DateTimeOffset` | When the event was committed |

---

## Correlation and Metadata Propagation

Wolverine creates a nested DI scope per handler, so `IDocumentSession` injected there is **not** the same instance as the HTTP-scoped one. Metadata (correlation/causation IDs) must be explicitly copied. Use a `Before` middleware:

```csharp
public class WolverineCorrelationMiddleware
{
    public static void Before(
        IDocumentSession session,
        IMessageContext context,
        ILogger<WolverineCorrelationMiddleware> logger,
        IHttpContextAccessor httpContextAccessor)
    {
        var httpContext = httpContextAccessor.HttpContext;

        // Priority: HttpContext.Items → X-Correlation-ID header → Wolverine CorrelationId → Activity tag
        var correlationId =
            httpContext?.Items["CorrelationId"] as string
            ?? httpContext?.Request.Headers["X-Correlation-ID"].FirstOrDefault()
            ?? context.CorrelationId;

        if (!string.IsNullOrEmpty(correlationId))
            session.CorrelationId = correlationId;

        // CausationId falls back to the Wolverine envelope message ID
        var causationId =
            httpContext?.Items["CausationId"] as string
            ?? context.Envelope?.Id.ToString();

        if (!string.IsNullOrEmpty(causationId))
            session.CausationId = causationId;

        // Additional audit headers stored in Marten event metadata
        session.SetHeader("user-id", httpContext?.User?.GetUserId().ToString() ?? "");
        session.SetHeader("remote-ip", httpContext?.Connection.RemoteIpAddress?.ToString() ?? "");
    }
}
```

Register in Wolverine:

```csharp
opts.Policies.AddMiddleware(typeof(WolverineCorrelationMiddleware));
```

---

## Wolverine Middleware Conventions

Wolverine middleware uses **static methods** with conventional names, not interfaces:

| Method | When it runs |
|---|---|
| `static void Before(...)` | Before the handler — use for setup, validation, header propagation |
| `static void After(...)` | After the handler (success path) |
| `static void Finally(...)` | Always (even on exception) — use for cleanup |

Parameters are resolved from DI. Any parameter type Wolverine knows about (the message, `IDocumentSession`, `IMessageContext`, etc.) can be declared.

```csharp
// ETag middleware — reads If-Match header and writes to the command
public static class WolverineETagMiddleware
{
    public static void Before(IMessageContext context, IHttpContextAccessor httpContextAccessor)
    {
        var message = context.Envelope?.Message;
        if (message is IHaveETag command)
        {
            var ifMatch = httpContextAccessor.HttpContext?
                .Request.Headers["If-Match"].FirstOrDefault();
            if (!string.IsNullOrEmpty(ifMatch))
                command.ETag = ifMatch;
        }
    }
}
```

---

## Handler Discovery

Wolverine discovers handlers by assembly scan or by explicit type registration. Prefer explicit registration so the assembly boundary is enforced by the compiler:

```csharp
opts.Discovery.IncludeAssembly(typeof(Program).Assembly);  // broad scan

// Fine-grained — register specific static classes
opts.Discovery.IncludeType(typeof(BookHandlers));
opts.Discovery.IncludeType(typeof(AuthorHandlers));
opts.Discovery.IncludeType(typeof(SaleHandlers));
```

---

## Durability and Dead-Letter Queue

```csharp
opts.Durability.EnableInboxPartitioning = true;    // performance boost — requires downtime to enable on existing systems
opts.Durability.InboxStaleTime  = TimeSpan.FromMinutes(10);
opts.Durability.OutboxStaleTime = TimeSpan.FromMinutes(10);
opts.EnableAutomaticFailureAcks = false;           // suppress automatic ack on handler failure
opts.UnknownMessageBehavior = UnknownMessageBehavior.DeadLetterQueue;  // park unrecognised messages for recovery
```

---

## IMessageContext vs IMessageBus

| | `IMessageContext` | `IMessageBus` |
|---|---|---|
| **Scope** | Per Wolverine handler execution | Application-scoped |
| **Inject in** | Handlers, middleware | Endpoints, background services, non-handler code |
| **Carries** | Current envelope, tenant ID, correlation ID | None |
| **Use for** | Publishing inside a handler, scheduling from event subscriptions | Dispatching commands from HTTP endpoints |

In a handler that reacts to Marten events, always use `IMessageContext` — it already carries the originating tenant ID:

```csharp
public static async Task Handle(
    IEvent<BookSaleScheduled> wrapper,
    IMessageContext bus)          // ← IMessageContext, not IMessageBus
{
    await bus.ScheduleAsync(..., wrapper.Data.Sale.Start);
}
```

---

## Common Mistakes

| Mistake | Fix |
|---|---|
| Calling `session.SaveChangesAsync()` inside a handler | Remove it — `AutoApplyTransactions()` commits after the handler returns |
| Using bare `TEventType` (not `IEvent<T>`) as the first parameter in an event-subscription handler | Wrap in `IEvent<TEventType>` to receive Marten metadata (tenant, stream id, version) |
| Forgetting `wrapper.TenantId` when scheduling a downstream command | Always pass `wrapper.TenantId` as part of the command so the downstream handler opens the correct tenant session |
| Injecting `IMessageBus` into a handler that needs to know the current tenant | Use `IMessageContext` instead — it exposes `TenantId` from the envelope |
| Registering `IDocumentSession` only via `UseLightweightSessions()` in multi-tenant projects | Add the explicit `AddScoped<IDocumentSession>` registration that reads `IMessageContext.TenantId` first |
| Adding middleware that relies on `HttpContext` for event-subscription handlers | Guard with a null check — event-subscription handlers have no `HttpContext` |

---

See also: [wolverine-etag.md](wolverine-etag.md), [wolverine-advanced.md](wolverine-advanced.md)
