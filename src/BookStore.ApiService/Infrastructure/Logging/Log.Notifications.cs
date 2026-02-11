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

        [LoggerMessage(
            EventId = 6009,
            Level = LogLevel.Information,
            Message = "Subscribed to Redis channel: {Channel}")]
        public static partial void SubscribedToRedis(ILogger logger, string channel);

        [LoggerMessage(
            EventId = 6010,
            Level = LogLevel.Information,
            Message = "Published {EventType} for entity {EntityId} to Redis")]
        public static partial void PublishedToRedis(ILogger logger, string eventType, Guid entityId);

        [LoggerMessage(
            EventId = 6011,
            Level = LogLevel.Warning,
            Message = "Redis not connected, SSE will work in single-instance mode only")]
        public static partial void RedisNotConnected(ILogger logger);

        [LoggerMessage(
            EventId = 6012,
            Level = LogLevel.Warning,
            Message = "Redis unavailable, falling back to local subscribers")]
        public static partial void RedisFallback(ILogger logger);

        [LoggerMessage(
            EventId = 6013,
            Level = LogLevel.Error,
            Message = "Failed to process Redis notification")]
        public static partial void FailedToProcessRedisMessage(ILogger logger, Exception ex);

        [LoggerMessage(
            EventId = 6014,
            Level = LogLevel.Information,
            Message = "Unsubscribed from Redis channel: {Channel}")]
        public static partial void UnsubscribedFromRedis(ILogger logger, string channel);

        [LoggerMessage(
            EventId = 6015,
            Level = LogLevel.Warning,
            Message = "Error unsubscribing from Redis during disposal")]
        public static partial void FailedToUnsubscribeFromRedis(ILogger logger, Exception ex);

        [LoggerMessage(
            EventId = 6016,
            Level = LogLevel.Debug,
            Message = "Failed to send heartbeat ping")]
        public static partial void FailedToSendHeartbeat(ILogger logger, Exception ex);

        [LoggerMessage(
            EventId = 6017,
            Level = LogLevel.Information,
            Message = "Broadcasting {EventType} for {EntityId} to local subscribers")]
        public static partial void BroadcastingToLocal(ILogger logger, string eventType, Guid entityId);

        [LoggerMessage(
            EventId = 6018,
            Level = LogLevel.Information,
            Message = "Publishing to Redis: {Json}")]
        public static partial void PublishingToRedis(ILogger logger, string json);
    }
}
