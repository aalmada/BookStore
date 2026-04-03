# INotificationService — Channel-Based Pub/Sub

## Interface

```csharp
public interface INotificationService
{
    /// <summary>Broadcasts a notification to all active subscribers.</summary>
    ValueTask NotifyAsync(IDomainEventNotification notification, CancellationToken ct = default);

    /// <summary>Returns a per-client stream of notifications.</summary>
    IAsyncEnumerable<SseItem<IDomainEventNotification>> Subscribe(CancellationToken ct);
}
```

## In-process implementation (single instance)

Uses `ConcurrentDictionary<Guid, Channel<...>>` — one `Channel` per connected client. `NotifyAsync` fans out to every subscriber. `Subscribe` is an async iterator that owns the channel's lifetime and removes itself on completion or cancellation.

```csharp
public class NotificationService : INotificationService
{
    readonly ConcurrentDictionary<Guid, Channel<SseItem<IDomainEventNotification>>> _subscribers = new();
    readonly ILogger<NotificationService> _logger;

    public NotificationService(ILogger<NotificationService> logger) => _logger = logger;

    public async ValueTask NotifyAsync(IDomainEventNotification notification, CancellationToken ct = default)
    {
        var sseItem = new SseItem<IDomainEventNotification>(notification, notification.EventType);

        foreach (var subscriber in _subscribers)
        {
            try
            {
                await subscriber.Value.Writer.WriteAsync(sseItem, ct);
            }
            catch (Exception ex)
            {
                Log.Notifications.FailedToSend(_logger, ex, subscriber.Key);
                _ = _subscribers.TryRemove(subscriber.Key, out _);
            }
        }
    }

    public async IAsyncEnumerable<SseItem<IDomainEventNotification>> Subscribe(
        [EnumeratorCancellation] CancellationToken ct)
    {
        var id = Guid.CreateVersion7();
        var channel = Channel.CreateUnbounded<SseItem<IDomainEventNotification>>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true  // only NotifyAsync writes; adjust if needed
        });

        _ = _subscribers.TryAdd(id, channel);

        try
        {
            await foreach (var item in channel.Reader.ReadAllAsync(ct))
            {
                yield return item;
            }
        }
        finally
        {
            _ = _subscribers.TryRemove(id, out _);
            channel.Writer.TryComplete();
        }
    }
}
```

## Key design decisions

**Why `ConcurrentDictionary` + `Channel` instead of `IObservable` or events?**
- `Channel<T>` is async-friendly and backpressure-aware. No thread-blocking.
- Each subscriber gets its own queue — a slow reader can't starve others.
- `[EnumeratorCancellation]` on the `CancellationToken` parameter ensures the async iterator responds to client disconnects immediately; ASP.NET Core passes the request's `CancellationToken` here automatically.

**Why `UnboundedChannel` rather than bounded?**
- SSE clients are typically long-lived; bounded channels add complexity (what happens when full?). Use bounded channels only if slow consumers are a concern and you want to drop or block.

## Registration

Register as a singleton — all requests share one instance for fan-out to work:

```csharp
services.AddSingleton<INotificationService, NotificationService>();
```

For multi-instance deployments, replace with `RedisNotificationService` — see [redis-scaling.md](redis-scaling.md).

## Triggering from Marten (ProjectionCommitListener)

The listener fires when async projections are committed by the Wolverine-managed daemon. It handles cache invalidation and SSE broadcast atomically:

```csharp
public class ProjectionCommitListener : IDocumentSessionListener, IChangeListener
{
    readonly HybridCache _cache;
    readonly INotificationService _notificationService;

    public async Task AfterCommitAsync(IDocumentSession session, IChangeSet commit, CancellationToken token)
    {
        await ProcessDocumentChangesAsync(commit.Inserted, ChangeType.Insert, token);
        await ProcessDocumentChangesAsync(commit.Updated, ChangeType.Update, token);
        await ProcessDocumentChangesAsync(commit.Deleted, ChangeType.Delete, token);
    }

    async Task HandleBookChangeAsync(BookProjection book, ChangeType changeType, CancellationToken token)
    {
        var effectiveType = DetermineEffectiveChangeType(changeType, book.IsDeleted);

        // 1. Invalidate cache
        await _cache.RemoveByTagAsync(CacheTags.BookItemPrefix + book.Id, token);
        await _cache.RemoveByTagAsync(CacheTags.BookList, token);

        // 2. Broadcast SSE notification
        IDomainEventNotification notification = effectiveType switch
        {
            ChangeType.Insert => new BookCreatedNotification(Guid.CreateVersion7(), book.Id, book.Title, DateTimeOffset.UtcNow),
            ChangeType.Update => new BookUpdatedNotification(Guid.CreateVersion7(), book.Id, book.Title, DateTimeOffset.UtcNow),
            ChangeType.Delete => new BookDeletedNotification(Guid.CreateVersion7(), book.Id, book.Title, DateTimeOffset.UtcNow),
            _ => throw new ArgumentOutOfRangeException(nameof(effectiveType))
        };
        await _notificationService.NotifyAsync(notification, token);
    }
}
```

Register the listener in Marten and as `IChangeListener` so async projections trigger it:

```csharp
opts.Events.AddEventListener(new ProjectionCommitListener(cache, notifications, logger));
opts.Events.Projections.Subscribe(new ProjectionCommitListener(cache, notifications, logger));
```

**Adding a new entity type**: Add a `Handle<TProjection>Async` method mirroring the pattern above. Don't forget to add it in `AfterCommitAsync`.
