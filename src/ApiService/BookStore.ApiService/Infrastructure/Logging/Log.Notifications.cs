using BookStore.ApiService.Infrastructure.Notifications;
using Microsoft.Extensions.Logging;

namespace BookStore.ApiService.Infrastructure.Logging;

public static partial class Log
{
    public static partial class Notifications
    {
        [LoggerMessage(
            EventId = 6001,
            Level = LogLevel.Information,
            Message = "NotifyAsync: {EventType} for {EntityId}")]
        public static partial void NotifyAsync(ILogger logger, string eventType, Guid entityId);

        [LoggerMessage(
            EventId = 6002,
            Level = LogLevel.Information,
            Message = "Client {SubscriberId} subscribed. Total subscribers: {Count}")]
        public static partial void ClientSubscribed(ILogger logger, Guid subscriberId, int count);

        [LoggerMessage(
            EventId = 6003,
            Level = LogLevel.Debug,
            Message = "Sending {EventType} to client {SubscriberId}")]
        public static partial void SendingEvent(ILogger logger, string eventType, Guid subscriberId);

        [LoggerMessage(
            EventId = 6004,
            Level = LogLevel.Information,
            Message = "Client {SubscriberId} unsubscribed. Remaining: {Count}")]
        public static partial void ClientUnsubscribed(ILogger logger, Guid subscriberId, int count);

        [LoggerMessage(
            EventId = 6005,
            Level = LogLevel.Warning,
            Message = "Failed to send notification to subscriber {SubscriberId}")]
        public static partial void FailedToSend(ILogger logger, Exception ex, Guid subscriberId);

        [LoggerMessage(
            EventId = 6006,
            Level = LogLevel.Information,
            Message = "Client connected to notification stream")]
        public static partial void ClientConnected(ILogger logger);

        [LoggerMessage(
            EventId = 6007,
            Level = LogLevel.Information,
            Message = "Creating subscription...")]
        public static partial void CreatingSubscription(ILogger logger);

        [LoggerMessage(
            EventId = 6008,
            Level = LogLevel.Information,
            Message = "Subscription created, returning SSE stream")]
        public static partial void SubscriptionCreated(ILogger logger);
    }
}
