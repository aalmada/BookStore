# Marten Greenfield Configuration Reference

> **Source:** Jeremy Miller (JasperFx), [Building a Greenfield System with the Critter Stack](https://jeremydmiller.com/2026/02/02/building-a-greenfield-system-with-the-critter-stack/), February 2, 2026

Opt-in settings recommended for every greenfield Marten project. They are not Marten defaults because enabling them on an _existing_ system requires a database schema migration.

> ⚠️ Do not remove or downgrade these settings without a migration plan — several require schema changes.

### Event Append Mode

```csharp
options.Events.AppendMode = EventAppendMode.Rich;
```

- **Why**: Improves throughput and reduces "event skipping" caused by concurrent appends.
- **Note**: Jeremy Miller recommends `EventAppendMode.Quick` (or `QuickWithServerTimestamps` if exact timestamps matter) for ~50% throughput improvement over the default. `Rich` retains richer metadata on every appended event at some throughput cost. Choose based on your requirements; do not change on a live system without benchmarking.
- **Requires schema migration**: Yes — changing `AppendMode` on an existing event store is a breaking change.

### Archived Stream Partitioning

```csharp
options.Events.UseArchivedStreamPartitioning = true;
```

- **Why**: Partitions archived streams into a separate PostgreSQL table partition, keeping active stream queries fast as data grows.
- **Jeremy says**: "100% do this."
- **Requires schema migration**: Yes.

### Advanced Async Tracking

```csharp
options.Events.EnableAdvancedAsyncTracking = true;
```

- **Why**: Provides better tracking of async daemon progress, which helps "heal" systems that encounter projection lag or failures.
- **Requires schema migration**: Yes.

### Event Skipping in Projections

```csharp
options.Events.EnableEventSkippingInProjectionsOrSubscriptions = true;
```

- **Why**: Allows marking individual events as "bad" (e.g., corrupt or schema-mismatch) so the async daemon skips them in projections from that point forward — without stopping the daemon.
- **Requires schema migration**: Yes.

### Identity Map for Aggregates

```csharp
options.Events.UseIdentityMapForAggregates = true;
```

- **Why**: Caches aggregate instances within the same session using an identity map. This optimises **inline projections** (aggregates used as write models inside a single Marten session) and is the cornerstone of the Decider Pattern / Aggregate Handler Workflow.
- **Effect on code**: Treat aggregate write models as immutable — load them via `session.Events.AggregateStreamAsync<T>()` once, operate on them, then append events. Never modify and reload in the same session.
- **Requires schema migration**: No, but changing this on a live system can affect inline projection behaviour.

### Mandatory Stream Type Declaration

```csharp
options.Events.UseMandatoryStreamTypeDeclaration = true;
```

- **Why**: Future-proofs the schema for planned Marten rebuild optimisations. Every stream must declare a type when started via `StartStream<TAggregate>()`.
- **Effect on code**: Always call `session.Events.StartStream<TAggregate>(id, @event)` — never the untyped overload.
- **Requires schema migration**: Yes.

### Disable Npgsql Logging

```csharp
options.DisableNpgsqlLogging = true;
```

- **Why**: The low-level PostgreSQL driver logs are noisy and rarely useful for application-level debugging. All meaningful events are tracked via Marten's own telemetry.
- **Requires schema migration**: No.

### Lightweight Sessions

```csharp
.UseLightweightSessions()
```

- **Why**: Eliminates identity-map overhead on every session. With the idiomatic Critter Stack / Wolverine handler pattern, handlers are short-lived and stateless — there are no deep call stacks that share document references across a session, so the identity map is never needed.
- **Jeremy says**: "With the idiomatic Critter Stack usage, the identity map is just overhead and I'd always opt into lightweight sessions for a new system."
- **Effect on code**: Sessions obtained via `IDocumentSession` are lightweight by default. Do not attempt to multi-load the same document twice and expect reference equality.
- **Requires schema migration**: No, but switching modes on an existing system can change behaviour for code that relies on identity-map semantics.

---

## Marten–Wolverine Integration

```csharp
.IntegrateWithWolverine(x =>
    x.UseWolverineManagedEventSubscriptionDistribution = true)
```

- **Why**: Lets Wolverine manage distribution of the Marten async daemon subscriptions across nodes rather than Marten doing it independently. This gives better load balancing across multiple API instances and integrates projection health into Wolverine's durability reporting.

---

## Related

- Wolverine durability settings → [`/wolverine__guide/configuration.md`](../wolverine__guide/configuration.md)
