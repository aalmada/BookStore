using Microsoft.Extensions.Logging;

namespace BookStore.ApiService.Infrastructure.Logging;

/// <summary>
/// Publisher-related log messages for CRUD operations and validation.
/// </summary>
public static partial class Log
{
    public static partial class Publishers
    {
        // Creation
        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Creating publisher: Id={PublisherId}, Name={Name}, CorrelationId={CorrelationId}")]
        public static partial void PublisherCreating(
            ILogger logger,
            Guid publisherId,
            string name,
            string correlationId);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Publisher created successfully: Id={PublisherId}, Name={Name}")]
        public static partial void PublisherCreated(
            ILogger logger,
            Guid publisherId,
            string name);

        // Update
        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Updating publisher: Id={PublisherId}, Name={Name}, Version={Version}")]
        public static partial void PublisherUpdating(
            ILogger logger,
            Guid publisherId,
            string name,
            long version);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Publisher updated successfully: Id={PublisherId}")]
        public static partial void PublisherUpdated(ILogger logger, Guid publisherId);

        // Soft Delete
        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Soft deleting publisher: Id={PublisherId}")]
        public static partial void PublisherSoftDeleting(ILogger logger, Guid publisherId);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Publisher soft deleted successfully: Id={PublisherId}")]
        public static partial void PublisherSoftDeleted(ILogger logger, Guid publisherId);

        // Restore
        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Restoring publisher: Id={PublisherId}")]
        public static partial void PublisherRestoring(ILogger logger, Guid publisherId);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Publisher restored successfully: Id={PublisherId}")]
        public static partial void PublisherRestored(ILogger logger, Guid publisherId);

        // ETag Validation
        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "ETag mismatch for publisher: Id={PublisherId}, Expected={ExpectedETag}, Provided={ProvidedETag}")]
        public static partial void ETagMismatch(
            ILogger logger,
            Guid publisherId,
            string expectedETag,
            string providedETag);

        // Not Found
        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Publisher not found: Id={PublisherId}")]
        public static partial void PublisherNotFound(ILogger logger, Guid publisherId);

        // Query Operations
        [LoggerMessage(
            Level = LogLevel.Debug,
            Message = "Retrieving publisher: Id={PublisherId}")]
        public static partial void RetrievingPublisher(ILogger logger, Guid publisherId);

        [LoggerMessage(
            Level = LogLevel.Debug,
            Message = "Listing publishers: Page={Page}, PageSize={PageSize}")]
        public static partial void ListingPublishers(ILogger logger, int page, int pageSize);
    }
}
