using Microsoft.Extensions.Logging;

namespace BookStore.ApiService.Infrastructure.Logging;

/// <summary>
/// Book-related log messages for CRUD operations, validation, and queries.
/// </summary>
public static partial class Log
{
    public static partial class Books
    {
        // Creation
        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Creating book: Id={BookId}, Title={Title}, CorrelationId={CorrelationId}")]
        public static partial void BookCreating(
            ILogger logger,
            Guid bookId,
            string title,
            string correlationId);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Book created successfully: Id={BookId}, Title={Title}")]
        public static partial void BookCreated(
            ILogger logger,
            Guid bookId,
            string title);

        // Update
        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Updating book: Id={BookId}, Title={Title}, Version={Version}")]
        public static partial void BookUpdating(
            ILogger logger,
            Guid bookId,
            string title,
            long version);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Book updated successfully: Id={BookId}")]
        public static partial void BookUpdated(ILogger logger, Guid bookId);

        // Soft Delete
        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Soft deleting book: Id={BookId}")]
        public static partial void BookSoftDeleting(ILogger logger, Guid bookId);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Book soft deleted successfully: Id={BookId}")]
        public static partial void BookSoftDeleted(ILogger logger, Guid bookId);

        // Restore
        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Restoring book: Id={BookId}")]
        public static partial void BookRestoring(ILogger logger, Guid bookId);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Book restored successfully: Id={BookId}")]
        public static partial void BookRestored(ILogger logger, Guid bookId);

        // Validation Errors
        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Invalid language code for book: BookId={BookId}, LanguageCode={LanguageCode}")]
        public static partial void InvalidLanguageCode(
            ILogger logger,
            Guid bookId,
            string languageCode);

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Invalid translation language codes for book: BookId={BookId}, InvalidCodes={InvalidCodes}")]
        public static partial void InvalidTranslationCodes(
            ILogger logger,
            Guid bookId,
            string invalidCodes);

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Missing default language translation for book: BookId={BookId}, DefaultLanguage={DefaultLanguage}")]
        public static partial void MissingDefaultTranslation(
            ILogger logger,
            Guid bookId,
            string defaultLanguage);

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Description too long for book: BookId={BookId}, LanguageCode={LanguageCode}, MaxLength={MaxLength}, ActualLength={ActualLength}")]
        public static partial void DescriptionTooLong(
            ILogger logger,
            Guid bookId,
            string languageCode,
            int maxLength,
            int actualLength);

        // ETag Validation
        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "ETag mismatch for book: Id={BookId}, Expected={ExpectedETag}, Provided={ProvidedETag}")]
        public static partial void ETagMismatch(
            ILogger logger,
            Guid bookId,
            string expectedETag,
            string providedETag);

        // Not Found
        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Book not found: Id={BookId}")]
        public static partial void BookNotFound(ILogger logger, Guid bookId);

        // Query Operations
        [LoggerMessage(
            Level = LogLevel.Debug,
            Message = "Searching books: SearchTerm={SearchTerm}, Page={Page}, PageSize={PageSize}")]
        public static partial void SearchingBooks(
            ILogger logger,
            string? searchTerm,
            int page,
            int pageSize);

        [LoggerMessage(
            Level = LogLevel.Debug,
            Message = "Books search completed: ResultCount={ResultCount}, TotalCount={TotalCount}")]
        public static partial void SearchCompleted(
            ILogger logger,
            int resultCount,
            int totalCount);

        [LoggerMessage(
            Level = LogLevel.Debug,
            Message = "Retrieving book: Id={BookId}")]
        public static partial void RetrievingBook(ILogger logger, Guid bookId);

        // Domain Validation Errors
        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Invalid book data: BookId={BookId}, Error={Error}")]
        public static partial void InvalidBookData(
            ILogger logger,
            Guid bookId,
            string error);

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Invalid book operation: BookId={BookId}, Error={Error}")]
        public static partial void InvalidBookOperation(
            ILogger logger,
            Guid bookId,
            string error);
    }
}
