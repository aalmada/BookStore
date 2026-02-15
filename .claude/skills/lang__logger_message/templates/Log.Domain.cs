using Microsoft.Extensions.Logging;

namespace BookStore.ApiService.Infrastructure.Logging;

/// <summary>
/// {Domain}-related log messages for {operations description}.
/// </summary>
public static partial class Log
{
    public static partial class {Domain}
    {
        // Creation
        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Creating {entity}: Id={{EntityId}}, Name={{Name}}, CorrelationId={{CorrelationId}}")]
        public static partial void {Entity}Creating(
            ILogger logger,
            Guid entityId,
            string name,
            string correlationId);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "{Entity} created successfully: Id={{EntityId}}, Name={{Name}}")]
        public static partial void {Entity}Created(
            ILogger logger,
            Guid entityId,
            string name);

        [LoggerMessage(
            Level = LogLevel.Error,
            Message = "Failed to create {entity}: Name={{Name}}")]
        public static partial void {Entity}CreationFailed(
            ILogger logger,
            Exception ex,
            string name);

        // Update
        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Updating {entity}: Id={{EntityId}}, Name={{Name}}, Version={{Version}}")]
        public static partial void {Entity}Updating(
            ILogger logger,
            Guid entityId,
            string name,
            long version);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "{Entity} updated successfully: Id={{EntityId}}")]
        public static partial void {Entity}Updated(
            ILogger logger,
            Guid entityId);

        [LoggerMessage(
            Level = LogLevel.Error,
            Message = "Failed to update {entity}: Id={{EntityId}}")]
        public static partial void {Entity}UpdateFailed(
            ILogger logger,
            Exception ex,
            Guid entityId);

        // Deletion
        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Deleting {entity}: Id={{EntityId}}, Version={{Version}}")]
        public static partial void {Entity}Deleting(
            ILogger logger,
            Guid entityId,
            long version);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "{Entity} deleted successfully: Id={{EntityId}}")]
        public static partial void {Entity}Deleted(
            ILogger logger,
            Guid entityId);

        [LoggerMessage(
            Level = LogLevel.Error,
            Message = "Failed to delete {entity}: Id={{EntityId}}")]
        public static partial void {Entity}DeletionFailed(
            ILogger logger,
            Exception ex,
            Guid entityId);

        // Validation
        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "{Entity} not found: Id={{EntityId}}")]
        public static partial void {Entity}NotFound(
            ILogger logger,
            Guid entityId);

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Version mismatch for {entity}: Id={{EntityId}}, Expected={{ExpectedVersion}}, Actual={{ActualVersion}}")]
        public static partial void {Entity}VersionMismatch(
            ILogger logger,
            Guid entityId,
            long expectedVersion,
            long actualVersion);
    }
}
