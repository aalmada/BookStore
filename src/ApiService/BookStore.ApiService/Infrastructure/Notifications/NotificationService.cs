using System.Collections.Concurrent;
using System.Net.ServerSentEvents;
using System.Threading.Channels;
using BookStore.ApiService.Infrastructure.Logging;
using BookStore.Shared.Notifications;

namespace BookStore.ApiService.Infrastructure.Notifications;

/// <summary>
/// Service that manages real-time notifications via Server-Sent Events (SSE).
/// Provides a way to broadcast domain events to all connected clients.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Broadcasts a notification to all active subscribers.
    /// </summary>
    ValueTask NotifyAsync(IDomainEventNotification notification, CancellationToken ct = default);

    /// <summary>
    /// Returns a stream of notifications for a client.
    /// </summary>
    IAsyncEnumerable<SseItem<IDomainEventNotification>> Subscribe(CancellationToken ct);
}

public class NotificationService : INotificationService
{
    private readonly ConcurrentDictionary<Guid, Channel<SseItem<IDomainEventNotification>>> _subscribers = new();
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(ILogger<NotificationService> logger)
    {
        _logger = logger;
    }

    public async ValueTask NotifyAsync(IDomainEventNotification notification, CancellationToken ct = default)
    {
        var sseItem = new SseItem<IDomainEventNotification>(notification, notification.EventType);
        Log.Notifications.NotifyAsync(_logger, notification.EventType, notification.EntityId);

        foreach (var subscriber in _subscribers)
        {
            try
            {
                await subscriber.Value.Writer.WriteAsync(sseItem, ct);
            }
            catch (Exception ex)
            {
                Log.Notifications.FailedToSend(_logger, ex, subscriber.Key);
                _subscribers.TryRemove(subscriber.Key, out _);
            }
        }
    }

    public async IAsyncEnumerable<SseItem<IDomainEventNotification>> Subscribe([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var id = Guid.CreateVersion7();
        var channel = Channel.CreateUnbounded<SseItem<IDomainEventNotification>>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true
        });

        _subscribers.TryAdd(id, channel);
        Log.Notifications.ClientSubscribed(_logger, id, _subscribers.Count);

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
            _subscribers.TryRemove(id, out _);
            Log.Notifications.ClientUnsubscribed(_logger, id, _subscribers.Count);
        }
    }
}
