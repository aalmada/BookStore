using Microsoft.Extensions.Logging;

namespace BookStore.ApiService.Infrastructure.Logging;

/// <summary>
/// Author-related log messages for CRUD operations and validation.
/// </summary>
public static partial class Log
{
    public static partial class Authors
    {
        // Creation
        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Creating author: Id={AuthorId}, Name={Name}, CorrelationId={CorrelationId}")]
        public static partial void AuthorCreating(
            ILogger logger,
            Guid authorId,
            string name,
            string correlationId);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Author created successfully: Id={AuthorId}, Name={Name}")]
        public static partial void AuthorCreated(
            ILogger logger,
            Guid authorId,
            string name);

        // Update
        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Updating author: Id={AuthorId}, Name={Name}, Version={Version}")]
        public static partial void AuthorUpdating(
            ILogger logger,
            Guid authorId,
            string name,
            long version);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Author updated successfully: Id={AuthorId}")]
        public static partial void AuthorUpdated(ILogger logger, Guid authorId);

        // Soft Delete
        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Soft deleting author: Id={AuthorId}")]
        public static partial void AuthorSoftDeleting(ILogger logger, Guid authorId);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Author soft deleted successfully: Id={AuthorId}")]
        public static partial void AuthorSoftDeleted(ILogger logger, Guid authorId);

        // Restore
        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Restoring author: Id={AuthorId}")]
        public static partial void AuthorRestoring(ILogger logger, Guid authorId);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Author restored successfully: Id={AuthorId}")]
        public static partial void AuthorRestored(ILogger logger, Guid authorId);

        // Validation Errors
        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Invalid translation language codes for author: AuthorId={AuthorId}, InvalidCodes={InvalidCodes}")]
        public static partial void InvalidTranslationCodes(
            ILogger logger,
            Guid authorId,
            string invalidCodes);

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Missing default language translation for author: AuthorId={AuthorId}, DefaultLanguage={DefaultLanguage}")]
        public static partial void MissingDefaultTranslation(
            ILogger logger,
            Guid authorId,
            string defaultLanguage);

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Biography too long for author: AuthorId={AuthorId}, LanguageCode={LanguageCode}, MaxLength={MaxLength}, ActualLength={ActualLength}")]
        public static partial void BiographyTooLong(
            ILogger logger,
            Guid authorId,
            string languageCode,
            int maxLength,
            int actualLength);

        // ETag Validation
        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "ETag mismatch for author: Id={AuthorId}, Expected={ExpectedETag}, Provided={ProvidedETag}")]
        public static partial void ETagMismatch(
            ILogger logger,
            Guid authorId,
            string expectedETag,
            string providedETag);

        // Not Found
        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Author not found: Id={AuthorId}")]
        public static partial void AuthorNotFound(ILogger logger, Guid authorId);

        // Query Operations
        [LoggerMessage(
            Level = LogLevel.Debug,
            Message = "Retrieving author: Id={AuthorId}")]
        public static partial void RetrievingAuthor(ILogger logger, Guid authorId);

        [LoggerMessage(
            Level = LogLevel.Debug,
            Message = "Listing authors: Page={Page}, PageSize={PageSize}")]
        public static partial void ListingAuthors(ILogger logger, int page, int pageSize);
    }
}
