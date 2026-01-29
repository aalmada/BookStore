using Microsoft.Extensions.Logging;

namespace BookStore.ApiService.Infrastructure.Logging;

/// <summary>
/// Category-related log messages for CRUD operations and validation.
/// </summary>
public static partial class Log
{
    public static partial class Categories
    {
        // Creation
        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Creating category: Id={CategoryId}, CorrelationId={CorrelationId}")]
        public static partial void CategoryCreating(
            ILogger logger,
            Guid categoryId,
            string correlationId);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Category created successfully: Id={CategoryId}")]
        public static partial void CategoryCreated(ILogger logger, Guid categoryId);

        // Update
        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Updating category: Id={CategoryId}, Version={Version}")]
        public static partial void CategoryUpdating(
            ILogger logger,
            Guid categoryId,
            long version);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Category updated successfully: Id={CategoryId}")]
        public static partial void CategoryUpdated(ILogger logger, Guid categoryId);

        // Soft Delete
        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Soft deleting category: Id={CategoryId}")]
        public static partial void CategorySoftDeleting(ILogger logger, Guid categoryId);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Category soft deleted successfully: Id={CategoryId}")]
        public static partial void CategorySoftDeleted(ILogger logger, Guid categoryId);

        // Restore
        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Restoring category: Id={CategoryId}")]
        public static partial void CategoryRestoring(ILogger logger, Guid categoryId);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Category restored successfully: Id={CategoryId}")]
        public static partial void CategoryRestored(ILogger logger, Guid categoryId);

        // Validation Errors
        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Invalid translation language codes for category: CategoryId={CategoryId}, InvalidCodes={InvalidCodes}")]
        public static partial void InvalidTranslationCodes(
            ILogger logger,
            Guid categoryId,
            string invalidCodes);

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Category name too long: CategoryId={CategoryId}, LanguageCode={LanguageCode}, MaxLength={MaxLength}, ActualLength={ActualLength}")]
        public static partial void NameTooLong(
            ILogger logger,
            Guid categoryId,
            string languageCode,
            int maxLength,
            int actualLength);


        // ETag Validation
        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "ETag mismatch for category: Id={CategoryId}, Expected={ExpectedETag}, Provided={ProvidedETag}")]
        public static partial void ETagMismatch(
            ILogger logger,
            Guid categoryId,
            string expectedETag,
            string providedETag);

        // Not Found
        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Category not found: Id={CategoryId}")]
        public static partial void CategoryNotFound(ILogger logger, Guid categoryId);

        // Query Operations
        [LoggerMessage(
            Level = LogLevel.Debug,
            Message = "Retrieving category: Id={CategoryId}")]
        public static partial void RetrievingCategory(ILogger logger, Guid categoryId);

        [LoggerMessage(
            Level = LogLevel.Debug,
            Message = "Listing categories: Page={Page}, PageSize={PageSize}")]
        public static partial void ListingCategories(ILogger logger, int page, int pageSize);
    }
}
