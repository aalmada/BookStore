using Microsoft.Extensions.Logging;

namespace BookStore.Client.Logging;

public static partial class Log
{
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Failed to deserialize SSE item: {EventType}")]
    public static partial void SseDeserializationFailed(ILogger logger, Exception ex, string eventType);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Error in SSE stream. Retrying in 5 seconds...")]
    public static partial void SseStreamError(ILogger logger, Exception ex);
}
