using System.Collections.Concurrent;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using BookStore.ApiService.Infrastructure.Logging;
using BookStore.Shared.Notifications;
using StackExchange.Redis;

namespace BookStore.ApiService.Infrastructure.Notifications;

/// <summary>
/// Redis-backed notification service for multi-instance deployments.
/// Publishes notifications to Redis pub/sub and subscribes to broadcast them to local SSE clients.
/// Falls back to in-memory mode if Redis is unavailable.
/// </summary>
public class RedisNotificationService : INotificationService, IDisposable
{
    readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, Channel<SseItem<IDomainEventNotification>>>> _subscribers = new();
    readonly ConcurrentDictionary<string, bool> _redisSubscriptions = new(StringComparer.OrdinalIgnoreCase);
    readonly IConnectionMultiplexer? _redis;
    readonly ILogger<RedisNotificationService> _logger;
    const string ChannelName = "bookstore:notifications";

    public RedisNotificationService(
        IConnectionMultiplexer redis,
        ILogger<RedisNotificationService> logger)
    {
        _redis = redis;
        _logger = logger;

        // Start a background heartbeat to verify connectivity
        _ = Task.Run(async () =>
        {
            while (true)
            {
                await Task.Delay(10000);
                try
                {
                    await NotifyAsync(new PingNotification(), JasperFx.StorageConstants.DefaultTenantId);
                }
                catch (Exception ex)
                {
                    Log.Notifications.FailedToSendHeartbeat(_logger, ex);
                }
            }
        });
    }

    void EnsureRedisSubscription(string tenantId)
    {
        if (_redis == null || !_redis.IsConnected)
        {
            Log.Notifications.RedisNotConnected(_logger);
            return;
        }

        if (!_redisSubscriptions.TryAdd(tenantId, true))
        {
            return;
        }

        var channelName = GetTenantChannelName(tenantId);

        _redis.GetSubscriber().Subscribe(RedisChannel.Literal(channelName), async (channel, message) =>
        {
            try
            {
                var notification = JsonSerializer.Deserialize<IDomainEventNotification>(
                    message.ToString(),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (notification != null)
                {
                    // Broadcast to all local SSE subscribers
                    Log.Notifications.BroadcastingToLocal(_logger, notification.EventType, notification.EntityId);
                    await BroadcastToLocalSubscribersAsync(notification, tenantId);
                }
            }
            catch (Exception ex)
            {
                Log.Notifications.FailedToProcessRedisMessage(_logger, ex);
            }
        });

        Log.Notifications.SubscribedToRedis(_logger, channelName);
    }

    async ValueTask BroadcastToLocalSubscribersAsync(IDomainEventNotification notification, string tenantId)
    {
        var sseItem = new SseItem<IDomainEventNotification>(notification, notification.EventType);
        Log.Notifications.NotifyAsync(_logger, notification.EventType, notification.EntityId);

        if (!_subscribers.TryGetValue(tenantId, out var tenantSubscribers))
        {
            return;
        }

        foreach (var subscriber in tenantSubscribers)
        {
            try
            {
                await subscriber.Value.Writer.WriteAsync(sseItem);
            }
            catch (Exception ex)
            {
                Log.Notifications.FailedToSend(_logger, ex, subscriber.Key);
                _ = tenantSubscribers.TryRemove(subscriber.Key, out _);
            }
        }
    }

    public async ValueTask NotifyAsync(IDomainEventNotification notification, string tenantId, CancellationToken ct = default)
    {
        // Publish to Redis (will be received by all instances including this one)
        if (_redis?.IsConnected == true)
        {
            var json = JsonSerializer.Serialize(notification, typeof(IDomainEventNotification));
            Log.Notifications.PublishingToRedis(_logger, json);
            _ = await _redis.GetSubscriber().PublishAsync(RedisChannel.Literal(GetTenantChannelName(tenantId)), json);

            Log.Notifications.PublishedToRedis(_logger, notification.EventType, notification.EntityId);
        }
        else
        {
            // Fallback: notify local subscribers directly if Redis unavailable
            Log.Notifications.RedisFallback(_logger);
            await BroadcastToLocalSubscribersAsync(notification, tenantId);
        }
    }

    public async IAsyncEnumerable<SseItem<IDomainEventNotification>> Subscribe(
        string tenantId,
        [EnumeratorCancellation] CancellationToken ct)
    {
        EnsureRedisSubscription(tenantId);

        var id = Guid.CreateVersion7();
        var channel = Channel.CreateUnbounded<SseItem<IDomainEventNotification>>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false // Multiple writers (Redis + fallback)
        });

        var tenantSubscribers = _subscribers.GetOrAdd(tenantId, _ => new ConcurrentDictionary<Guid, Channel<SseItem<IDomainEventNotification>>>());
        _ = tenantSubscribers.TryAdd(id, channel);
        Log.Notifications.ClientSubscribed(_logger, id, tenantSubscribers.Count);

        try
        {
            await foreach (var item in channel.Reader.ReadAllAsync(ct))
            {
                Log.Notifications.SendingEvent(_logger, item.EventType, id);
                yield return item;
            }
        }
        finally
        {
            _ = tenantSubscribers.TryRemove(id, out _);
            Log.Notifications.ClientUnsubscribed(_logger, id, tenantSubscribers.Count);
        }
    }

    public void Dispose()
    {
        try
        {
            if (_redis is { IsConnected: true })
            {
                foreach (var tenantId in _redisSubscriptions.Keys)
                {
                    var channelName = GetTenantChannelName(tenantId);
                    _redis.GetSubscriber().Unsubscribe(RedisChannel.Literal(channelName));
                    Log.Notifications.UnsubscribedFromRedis(_logger, channelName);
                }
            }
        }
        catch (ObjectDisposedException)
        {
            // Redis connection already disposed, ignore
        }
        catch (Exception ex)
        {
            // Log warning but don't throw during dispose
            Log.Notifications.FailedToUnsubscribeFromRedis(_logger, ex);
        }
    }

    static string GetTenantChannelName(string tenantId) => $"{ChannelName}:{tenantId}";
}
