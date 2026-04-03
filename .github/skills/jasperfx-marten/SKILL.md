---
name: jasperfx-marten
description: Use for any request involving Marten (.NET document database and event store on PostgreSQL): document storage, event sourcing, projections, aggregates, multi-tenancy, LINQ queries, async daemon, commit listeners, natural keys, or Marten + Wolverine integration. Always trigger when the user mentions Marten, event sourcing, event store, stream projections, SingleStreamProjection, MultiStreamProjection, IDocumentSession, IQuerySession, async daemon, conjoined tenancy, or asks about building read models from events — even if they don't name Marten explicitly.
---

# Marten Skill

Marten turns PostgreSQL into both a document database and an event store. It is the data persistence layer in the Critter Stack alongside Wolverine.

**Key mental model**: Write model = events + aggregates. Read model = projections (built by the async daemon from events).

## Reference Index

| File | Topics |
|------|--------|
| [marten-setup.md](references/marten-setup.md) | NuGet setup, `AddMarten()`, connection, event registration, Wolverine integration, session types |
| [marten-documents.md](references/marten-documents.md) | Document storage (`Store/Load/Delete`), LINQ queries, text search, pagination, includes, natural keys |
| [marten-events.md](references/marten-events.md) | Events design, `StartStream/Append`, `AggregateStreamAsync`, `FetchStreamStateAsync`, aggregates, `ISoftDeleted` |
| [marten-projections.md](references/marten-projections.md) | `SingleStreamProjection`, `MultiStreamProjection`, lifecycle, registration, enrichment, composite projections |
| [marten-advanced.md](references/marten-advanced.md) | Multi-tenancy, async daemon, commit listeners, side effects, metadata, performance |

## Quick Reference

```csharp
// Session types
IDocumentSession   // read + write; auto-committed by Wolverine
IQuerySession      // read-only; inject for queries

// Events
session.Events.StartStream<TAggregate>(id, event1, event2);
session.Events.Append(id, @event);
var agg = await session.Events.AggregateStreamAsync<TAggregate>(id);
var state = await session.Events.FetchStreamStateAsync(id);

// Documents
session.Store(doc);
var doc = await session.LoadAsync<T>(id);
var list = await session.Query<T>().Where(...).ToListAsync();
```

## See Also

- [Official Marten Docs](https://martendb.io)
- [Marten Guide (project)](../../../../docs/guides/marten-guide.md)
- [Event Sourcing Guide (project)](../../../../docs/guides/event-sourcing-guide.md)
- [Wolverine skill](../wolverine/SKILL.md) for handler + transaction patterns
