---
name: aspnet-sse
description: Implement Server-Sent Events (SSE) in ASP.NET Core using TypedResults.ServerSentEvents, SseItem<T>, and Channel-based pub/sub — including the notification service pattern, multi-instance Redis scaling, and the SseParser client. Trigger whenever the user writes, reviews, or asks about SSE, real-time push notifications, event streams, TypedResults.ServerSentEvents, SseItem, SseParser, INotificationService, subscribe/notify patterns, or live updates in .NET or Blazor — even if they don't mention "SSE" or "Server-Sent Events" by name. Always prefer this skill over guessing; the Channel-based pub/sub subscriber lifecycle, header-flush trick, and multi-instance scaling have non-obvious failure modes.
---

# ASP.NET Core Server-Sent Events (SSE) Skill

SSE is the right choice for server-to-client real-time push when bi-directional communication isn't needed — notifications, live updates, AI streaming. Unlike WebSockets, SSE runs over plain HTTP/1.1, is trivially proxied, and gets free reconnect logic in browsers.

ASP.NET Core 10 adds native first-class support via `TypedResults.ServerSentEvents` and `System.Net.ServerSentEvents.SseItem<T>`, eliminating the need for manual `text/event-stream` formatting.

## Quick reference

| Topic | See |
|-------|-----|
| Server endpoint: `TypedResults.ServerSentEvents`, `SseItem<T>`, initial-event flush | [server-endpoint.md](references/server-endpoint.md) |
| In-process notification service: `Channel<T>`, `ConcurrentDictionary`, subscriber lifecycle | [notification-service.md](references/notification-service.md) |
| Multi-instance scaling with Redis pub/sub | [redis-scaling.md](references/redis-scaling.md) |
| Client consumption: `SseParser`, `IAsyncEnumerable`, reconnect | [client.md](references/client.md) |

## Architecture overview

```
Mutation → Event stored → Projection updated → INotificationService.NotifyAsync()
                                                    ↓ (fan-out)
                                        Channel per subscriber → SseItem<T>
                                                    ↓
                                        TypedResults.ServerSentEvents(stream)
                                                    ↓
                                        Browser EventSource / SseParser
```

For multi-instance deployments, `NotifyAsync` publishes to Redis pub/sub instead of writing to local channels directly. Each instance subscribes to Redis and fans out to its own local `Channel` subscribers. See [redis-scaling.md](references/redis-scaling.md).

## Core types

| Type | Namespace | Purpose |
|------|-----------|---------|
| `TypedResults.ServerSentEvents` | `Microsoft.AspNetCore.Http` | Returns SSE result from endpoint |
| `SseItem<T>` | `System.Net.ServerSentEvents` | Wraps payload with event type, id, retry |
| `SseParser` | `System.Net.ServerSentEvents` | Parses SSE stream on client (Blazor/.NET) |
| `Channel<T>` | `System.Threading.Channels` | Per-subscriber unbounded queue |

## The three overloads of TypedResults.ServerSentEvents

```csharp
// 1. Strings — sent as raw text (no JSON wrapping)
TypedResults.ServerSentEvents(IAsyncEnumerable<string> values, string? eventType = null)

// 2. Objects — serialized as JSON
TypedResults.ServerSentEvents<T>(IAsyncEnumerable<T> values, string? eventType = null)

// 3. SseItem<T> — full control over event type, id, and data per item
TypedResults.ServerSentEvents<T>(IAsyncEnumerable<SseItem<T>> values)
```

Use overload 3 (`SseItem<T>`) when each event has a different type (e.g., `BookCreated`, `AuthorUpdated`). The event type becomes the `event:` field in the SSE wire format and maps directly to `EventSource.addEventListener('BookCreated', ...)` in JavaScript.

## Pattern: polymorphic domain events

When you have multiple event types sharing a base interface, use `[JsonPolymorphic]` on the interface and `[JsonDerivedType]` for each concrete type. This lets you serialize/deserialize through `IDomainEventNotification` without losing type information:

```csharp
[JsonPolymorphic(TypeDiscriminatorPropertyName = "NotificationType")]
[JsonDerivedType(typeof(BookCreatedNotification), "BookCreated")]
[JsonDerivedType(typeof(BookUpdatedNotification), "BookUpdated")]
public interface IDomainEventNotification
{
    Guid EventId { get; }
    Guid EntityId { get; }
    string EventType { get; }
    DateTimeOffset Timestamp { get; }
}
```

Each notification record is past-tense, carries `EventId` for causation tracking, and has a stable `EventType` string that matches the SSE event name.

## Common mistakes

- **No initial event → browser hangs**: Browsers buffer `text/event-stream` responses until they see data. Emit a `ping`/`Connected` event immediately when a client subscribes to flush headers. See [server-endpoint.md](references/server-endpoint.md#header-flush-trick).
- **Shared mutable state without CancellationToken**: Always thread `CancellationToken` through `Channel.Writer.WriteAsync` and use `[EnumeratorCancellation]` on the subscribe method. Otherwise clients that disconnect silently leak channel entries forever.
- **Single `INotificationService` instance in multi-replica deployments**: Local channels don't cross process boundaries. Add Redis pub/sub or a message broker. See [redis-scaling.md](references/redis-scaling.md).
- **Missing `ProjectionCommitListener` registration**: If SSE notifications don't fire after a mutation, check that the new projection type is handled in `AfterCommitAsync`. The listener must be registered as both `IDocumentSessionListener` and `IChangeListener` in Marten configuration.
- **`Results` vs `TypedResults`**: Prefer `TypedResults.ServerSentEvents` — it returns the concrete `ServerSentEventsResult<T>` which integrates with OpenAPI metadata automatically. `Results.ServerSentEvents` returns `IResult` and loses that.
