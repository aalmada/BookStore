# Wolverine Greenfield Configuration Reference

> **Source:** Jeremy Miller (JasperFx), [Building a Greenfield System with the Critter Stack](https://jeremydmiller.com/2026/02/02/building-a-greenfield-system-with-the-critter-stack/), February 2, 2026

Opt-in settings recommended for every greenfield Wolverine project. They are not Wolverine defaults because enabling them on an _existing_ system requires a schema migration or downtime.

> ⚠️ Do not remove or downgrade these settings without a migration plan — several require schema changes or a maintenance window.

---

## Inbox Partitioning

```csharp
opts.Durability.EnableInboxPartitioning = true;
```

- **Why**: Partitions the durable inbox table for higher throughput. Allows multiple nodes to process inbox messages concurrently without lock contention.
- **Requires downtime**: Yes — enabling on an existing system requires a migration and a scheduled maintenance window.

## Inbox / Outbox Stale Time

```csharp
opts.Durability.InboxStaleTime = TimeSpan.FromMinutes(10);
opts.Durability.OutboxStaleTime = TimeSpan.FromMinutes(10);
```

- **Why**: Marks unprocessed inbox/outbox records older than 10 minutes as stale so they can be cleaned up or retried, adding an extra layer of durability for edge-case failures (e.g., process crash mid-flight).
- **Requires schema migration**: Yes — adds columns to durability tables.

## Disable Automatic Failure Acks

```csharp
opts.EnableAutomaticFailureAcks = false;
```

- **Why**: Automatic failure acknowledgements are misleading — they acknowledge a message as "failed" immediately without giving the dead-letter pipeline a chance to handle it. Disabling this keeps failed messages available for inspection and retry.
- **Jeremy says**: "Just annoying."

## Unknown Message Behaviour

```csharp
opts.UnknownMessageBehavior = UnknownMessageBehavior.DeadLetterQueue;
```

- **Why**: Messages that arrive but have no registered handler are stored in the dead-letter queue rather than silently dropped, enabling later inspection and recovery.

---

## Related

- Marten event store settings and Marten–Wolverine integration → [`/marten__guide/configuration.md`](../marten__guide/configuration.md)
