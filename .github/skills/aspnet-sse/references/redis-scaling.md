# Multi-Instance Scaling with Redis Pub/Sub

In a single-process deployment, `NotificationService` fans out directly to in-memory `Channel` subscribers. When you scale horizontally (multiple API replicas), clients connected to replica A won't receive notifications triggered on replica B.

The fix is Redis pub/sub: each instance publishes to a shared Redis channel and subscribes to fan out to its own local SSE subscribers.

## RedisNotificationService

```csharp
public class RedisNotificationService : INotificationService, IDisposable
{
    readonly ConcurrentDictionary<Guid, Channel<SseItem<IDomainEventNotification>>> _subscribers = new();
    readonly IConnectionMultiplexer? _redis;
    readonly ILogger<RedisNotificationService> _logger;
    const string ChannelName = "bookstore:notifications";

    public RedisNotificationService(IConnectionMultiplexer redis, ILogger<RedisNotificationService> logger)
    {
        _redis = redis;
        _logger = logger;
        InitializeRedisSubscription();
    }

    void InitializeRedisSubscription()
    {
        if (_redis == null || !_redis.IsConnected) return;

        _redis.GetSubscriber().Subscribe(RedisChannel.Literal(ChannelName), async (_, message) =>
        {
            var notification = JsonSerializer.Deserialize<IDomainEventNotification>(
                message.ToString(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (notification != null)
                await BroadcastToLocalSubscribersAsync(notification);
        });
    }

    public async ValueTask NotifyAsync(IDomainEventNotification notification, CancellationToken ct = default)
    {
        if (_redis?.IsConnected == true)
        {
            // All instances receive this, including the current one
            var json = JsonSerializer.Serialize(notification, typeof(IDomainEventNotification));
            _ = await _redis.GetSubscriber().PublishAsync(RedisChannel.Literal(ChannelName), json);
        }
        else
        {
            // Graceful fallback: direct fan-out if Redis unavailable
            await BroadcastToLocalSubscribersAsync(notification);
        }
    }

    async ValueTask BroadcastToLocalSubscribersAsync(IDomainEventNotification notification)
    {
        var sseItem = new SseItem<IDomainEventNotification>(notification, notification.EventType);
        foreach (var subscriber in _subscribers)
        {
            try { await subscriber.Value.Writer.WriteAsync(sseItem); }
            catch { _ = _subscribers.TryRemove(subscriber.Key, out _); }
        }
    }

    public async IAsyncEnumerable<SseItem<IDomainEventNotification>> Subscribe(
        [EnumeratorCancellation] CancellationToken ct)
    {
        var id = Guid.CreateVersion7();
        var channel = Channel.CreateUnbounded<SseItem<IDomainEventNotification>>(
            new UnboundedChannelOptions { SingleReader = true });
        _ = _subscribers.TryAdd(id, channel);
        try
        {
            await foreach (var item in channel.Reader.ReadAllAsync(ct))
                yield return item;
        }
        finally
        {
            _ = _subscribers.TryRemove(id, out _);
            channel.Writer.TryComplete();
        }
    }

    public void Dispose() => _redis?.GetSubscriber().Unsubscribe(RedisChannel.Literal(ChannelName));
}
```

## Registration

```csharp
if (builder.Configuration.GetConnectionString("redis") is { } redisCs)
{
    services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisCs));
    services.AddSingleton<INotificationService, RedisNotificationService>();
}
else
{
    services.AddSingleton<INotificationService, NotificationService>();
}
```

## Serialization gotcha with polymorphism

When serializing `IDomainEventNotification` to Redis, pass `typeof(IDomainEventNotification)` (not `var`) as the type argument to `JsonSerializer.Serialize` — otherwise the type discriminator is omitted and deserialization produces nulls:

```csharp
// ✅ Correct — type discriminator included
var json = JsonSerializer.Serialize(notification, typeof(IDomainEventNotification));

// ❌ Wrong — T is inferred as concrete type, discriminator missing
var json = JsonSerializer.Serialize(notification);
```

On deserialization, pass the same options used at startup (or a shared static instance):

```csharp
JsonSerializer.Deserialize<IDomainEventNotification>(json, _sharedOptions);
```

## Heartbeat

The Redis service runs a background heartbeat that publishes a `PingNotification` every 10 seconds. This confirms Redis connectivity and keeps long-lived SSE connections from being cut by idle-timeout proxies.
