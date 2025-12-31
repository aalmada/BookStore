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
            Level = LogLevel.Debug,
            Message = "Setting Marten metadata: CorrelationId={CorrelationId}, CausationId={CausationId}, UserId={UserId}")]
        public static partial void MartenMetadataSet(
            ILogger logger,
            string correlationId,
            string causationId,
            string? userId);

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
            Level = LogLevel.Error,
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
    }
}
