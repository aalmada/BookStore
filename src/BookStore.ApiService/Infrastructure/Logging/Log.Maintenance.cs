namespace BookStore.ApiService.Infrastructure.Logging;

/// <summary>
/// High-performance logging for maintenance operations.
/// </summary>
public static partial class Log
{
    public static partial class Maintenance
    {
        [LoggerMessage(
            EventId = 5001,
            Level = LogLevel.Information,
            Message = "Starting unverified account cleanup. Expiration: {ExpirationHours} hours.")]
        public static partial void AccountCleanupStarted(ILogger logger, int expirationHours);

        [LoggerMessage(
            EventId = 5002,
            Level = LogLevel.Information,
            Message = "Unverified account cleanup completed. Deleted {DeletedCount} accounts.")]
        public static partial void AccountCleanupCompleted(ILogger logger, int deletedCount);

        [LoggerMessage(
            EventId = 5003,
            Level = LogLevel.Error,
            Message = "Error occurred during unverified account cleanup.")]
        public static partial void AccountCleanupFailed(ILogger logger, Exception ex);

        [LoggerMessage(
            EventId = 5004,
            Level = LogLevel.Information,
            Message = "Triggering initial unverified account cleanup job.")]
        public static partial void InitialCleanupTriggered(ILogger logger);

        [LoggerMessage(
            EventId = 5005,
            Level = LogLevel.Error,
            Message = "Failed to trigger initial unverified account cleanup job.")]
        public static partial void InitialCleanupTriggerFailed(ILogger logger, Exception ex);
    }
}
