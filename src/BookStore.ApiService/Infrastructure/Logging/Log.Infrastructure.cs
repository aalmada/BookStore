using Microsoft.Extensions.Logging;

namespace BookStore.ApiService.Infrastructure.Logging;

/// <summary>
/// Infrastructure-related log messages for middleware, startup, and system operations.
/// </summary>
public static partial class Log
{
    public static partial class Infrastructure
    {
        // Marten Metadata Middleware
        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Marten metadata set for {Method} {Path}: CorrelationId={CorrelationId}, CausationId={CausationId}, UserId={UserId}, RemoteIp={RemoteIp}")]
        public static partial void MartenMetadataApplied(
            ILogger logger,
            string method,
            string path,
            string correlationId,
            string causationId,
            string? userId,
            string? remoteIp);

        // Logging Enricher Middleware
        [LoggerMessage(
            Level = LogLevel.Debug,
            Message = "Request started: {Method} {Path} from {RemoteIp}")]
        public static partial void RequestStarted(
            ILogger logger,
            string method,
            string path,
            string remoteIp);

        // Database Seeding
        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Starting database seeding")]
        public static partial void DatabaseSeedingStarted(ILogger logger);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Database seeding completed successfully")]
        public static partial void DatabaseSeedingCompleted(ILogger logger);

        [LoggerMessage(
            Level = LogLevel.Critical,
            Message = "Database seeding failed")]
        public static partial void DatabaseSeedingFailed(ILogger logger, Exception exception);

        // Projection Initialization
        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Waiting for async projections to complete...")]
        public static partial void WaitingForProjections(ILogger logger);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "All projections are ready: {BookCount} books, {AuthorCount} authors, {CategoryCount} categories, {PublisherCount} publishers")]
        public static partial void ProjectionsReady(
            ILogger logger,
            int bookCount,
            int authorCount,
            int categoryCount,
            int publisherCount);

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Projection initialization timed out after {TimeoutSeconds}s. Some projections may not be ready.")]
        public static partial void ProjectionTimeout(ILogger logger, double timeoutSeconds);

        [LoggerMessage(
            Level = LogLevel.Debug,
            Message = "Checking projection status: Books={BookCount}, Authors={AuthorCount}, Categories={CategoryCount}, Publishers={PublisherCount}")]
        public static partial void ProjectionStatus(
            ILogger logger,
            int bookCount,
            int authorCount,
            int categoryCount,
            int publisherCount);

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Cache invalidation not implemented for projection type {ProjectionType}. Consider adding a case to handle this projection.")]
        public static partial void CacheInvalidationNotImplemented(ILogger logger, string projectionType);

        // Projection Commit Listener
        [LoggerMessage(
            Level = LogLevel.Debug,
            Message = "AfterCommitAsync called. Inserted: {InsertedCount}, Updated: {UpdatedCount}, Deleted: {DeletedCount}")]
        public static partial void AfterCommitAsync(
            ILogger logger,
            int insertedCount,
            int updatedCount,
            int deletedCount);

        [LoggerMessage(
            Level = LogLevel.Error,
            Message = "Error processing projection commit")]
        public static partial void ErrorProcessingProjectionCommit(ILogger logger, Exception exception);

        [LoggerMessage(
            Level = LogLevel.Debug,
            Message = "Processing {ChangeType}: {DocumentType}")]
        public static partial void ProcessingDocumentChange(
            ILogger logger,
            string changeType,
            string documentType);

        [LoggerMessage(
            Level = LogLevel.Error,
            Message = "Error processing {ChangeType} document of type {DocumentType}")]
        public static partial void ErrorProcessingDocumentChange(
            ILogger logger,
            Exception exception,
            string changeType,
            string documentType);

        [LoggerMessage(
            Level = LogLevel.Debug,
            Message = "Invalidated cache {ItemTag} and {ListTag}")]
        public static partial void CacheInvalidated(
            ILogger logger,
            string itemTag,
            string listTag);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Sending {NotificationType} for {EntityType}")]
        public static partial void SendingNotification(
            ILogger logger,
            string notificationType,
            string entityType);

        // Startup
        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Starting the API Service...")]
        public static partial void StartingApiService(ILogger logger);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Registering Marten events for the first time...")]
        public static partial void RegisteringMartenEvents(ILogger logger);

        [LoggerMessage(
            Level = LogLevel.Critical,
            Message = "Failed to register Marten events")]
        public static partial void FailedToRegisterMartenEvents(ILogger logger, Exception exception);

        [LoggerMessage(
            Level = LogLevel.Error,
            Message = "An error occurred during startup")]
        public static partial void StartupError(ILogger logger, Exception exception);

        [LoggerMessage(
            Level = LogLevel.Error,
            Message = "Unhandled exception: {Message}")]
        public static partial void UnhandledException(ILogger logger, Exception exception, string message);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "[WOLVERINE-CORRELATION] Session CorrelationId: {SessionId}, CausationId: {SessionCid} (HttpContext present: {HasContext})")]
        public static partial void WolverineCorrelation(
            ILogger logger,
            string? sessionId,
            string? sessionCid,
            bool hasContext);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Background startup task running. Environment: {Environment}, SeedingEnabled: {SeedingEnabled}")]
        public static partial void StartupTaskRunning(
            ILogger logger,
            string environment,
            bool seedingEnabled);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Seeding tenant: {TenantId}")]
        public static partial void SeedingTenant(
            ILogger logger,
            string tenantId);

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Database seeding failed (attempt {RetryCount}/{MaxRetries}). Retrying in {RetryDelay}s...")]
        public static partial void SeedingFailedRetrying(
            ILogger logger,
            Exception exception,
            int retryCount,
            int maxRetries,
            double retryDelay);

        [LoggerMessage(
            Level = LogLevel.Error,
            Message = "Database seeding failed after {RetryCount} attempts. Application may not behave correctly.")]
        public static partial void SeedingFailedMaxRetries(
            ILogger logger,
            Exception exception,
            int retryCount);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "[DEBUG_LISTENER] HandleUserChangeAsync for {UserId}. Favorites: {Count}")]
        public static partial void DebugHandleUserChange(
            ILogger logger,
            Guid userId,
            int count);
    }
}
