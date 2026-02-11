using Microsoft.Extensions.Logging;

namespace BookStore.Client.Logging;

public static partial class Log
{
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Failed to deserialize SSE item: {EventType}. Data: {Data}")]
    public static partial void SseDeserializationFailed(ILogger logger, string eventType, string data);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Error in SSE stream. Retrying in 5 seconds...")]
    public static partial void SseStreamError(ILogger logger, Exception ex);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Starting SSE event stream listening for base address: {BaseAddress}")]
    public static partial void SseStreamStarted(ILogger logger, Uri? baseAddress);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "SSE connection established successfully.")]
    public static partial void SseConnectionEstablished(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Domain event received: {EventType} for Entity {EntityId}")]
    public static partial void SseEventReceived(ILogger logger, string eventType, Guid entityId);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "SSE stream reached EndOfStream. Reconnecting...")]
    public static partial void SseEndOfStream(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "SSE listening stopped via cancellation.")]
    public static partial void SseListeningStopped(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Error processing individual SSE message.")]
    public static partial void SseProcessingError(ILogger logger, Exception ex);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "API Error: {Method} {Url} returned {StatusCode}. Content: {Content}")]
    public static partial void ApiError(ILogger logger, HttpMethod method, Uri? url, System.Net.HttpStatusCode statusCode, string content);
}
