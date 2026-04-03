# SSE Server Endpoint

## Endpoint registration

Set `contentType: "text/event-stream"` in the `.Produces(...)` call so OpenAPI reflects the correct media type. Use `TypedResults.ServerSentEvents` (not `Results.ServerSentEvents`) for the concrete return type so OpenAPI can infer the schema automatically.

```csharp
using System.Net.ServerSentEvents;
using Microsoft.AspNetCore.Http.HttpResults;

public static class NotificationEndpoints
{
    public static RouteGroupBuilder MapNotificationEndpoints(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/stream", GetNotificationStream)
            .WithName("GetNotificationStream")
            .WithSummary("Subscribe to real-time notifications via SSE")
            .Produces(StatusCodes.Status200OK, contentType: "text/event-stream");

        return group;
    }

    static IResult GetNotificationStream(
        INotificationService notificationService,
        ILogger<INotificationService> logger,
        CancellationToken cancellationToken)
    {
        var stream = notificationService.Subscribe(cancellationToken);
        var streamWithInit = EmitInitialEvent(stream);
        return TypedResults.ServerSentEvents(streamWithInit);
    }

    // Header-flush trick: yield one event immediately before the real stream
    static async IAsyncEnumerable<SseItem<IDomainEventNotification>> EmitInitialEvent(
        IAsyncEnumerable<SseItem<IDomainEventNotification>> source)
    {
        yield return new SseItem<IDomainEventNotification>(new PingNotification(), "Connected");
        await foreach (var item in source)
        {
            yield return item;
        }
    }
}
```

## Header-flush trick

HTTP/1.1 proxies and browsers buffer responses until enough data arrives. SSE streams would sit silent until the server sends at least one event. To prevent a perceived hang, yield a throwaway `ping`/`Connected` event at the very start of `Subscribe`. The client simply ignores or discards it.

## SseItem<T> anatomy

```csharp
var item = new SseItem<MyPayload>(data, eventType: "BookCreated")
{
    EventId = "evt-123",   // optional — used by client for Last-Event-ID reconnect
    ReconnectionInterval = TimeSpan.FromSeconds(5), // optional — tells client retry delay
};
```

| Field | Wire format | Purpose |
|-------|-------------|---------|
| `EventType` | `event: BookCreated` | Distinguishes event kinds; maps to `addEventListener` |
| `EventId` | `id: evt-123` | Client sends `Last-Event-ID` header on reconnect |
| `ReconnectionInterval` | `retry: 5000` | Overrides browser's default reconnect delay |
| `Data` | `data: {...}` | JSON-serialized (or raw for strings) payload |

## OpenAPI / Scalar display

`TypedResults.ServerSentEvents` implements `IEndpointMetadataProvider`, so the `200 OK` with `text/event-stream` is inferred. You still need the explicit `.Produces(...)` call to set the media type string in the OpenAPI document:

```csharp
.Produces(StatusCodes.Status200OK, contentType: "text/event-stream")
```

## Causation ID propagation

Include `EventId` in each notification record so the client can set `CausationId` on its next outgoing request — closing the audit trace from the original command through the async projection to the UI refresh:

```csharp
public record BookCreatedNotification(
    Guid EventId,   // the Marten event sequence ID
    Guid EntityId,
    string Title,
    DateTimeOffset Timestamp) : IDomainEventNotification
{
    public string EventType => "BookCreated";
}
```
