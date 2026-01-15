using Microsoft.Extensions.Logging;

namespace BookStore.ApiService.Infrastructure.Logging;

/// <summary>
/// Database seeding-related log messages.
/// </summary>
public static partial class Log
{
    public static partial class Seeding
    {
        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Database already seeded, skipping")]
        public static partial void DatabaseAlreadySeeded(ILogger logger);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Starting database seeding")]
        public static partial void StartingDatabaseSeeding(ILogger logger);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Database seeding completed successfully")]
        public static partial void DatabaseSeedingCompleted(ILogger logger);

        // Sales Seeding
        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Starting sales seeding")]
        public static partial void StartingSalesSeeding(ILogger logger);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Found {BookCount} books for sales seeding")]
        public static partial void FoundBooksForSalesSeeding(ILogger logger, int bookCount);

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "No books found for sales seeding")]
        public static partial void NoBooksFoundForSalesSeeding(ILogger logger);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Scheduled {Percentage}% sale for book {BookId} ({BookTitle})")]
        public static partial void ScheduledSale(
            ILogger logger,
            decimal percentage,
            Guid bookId,
            string bookTitle);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Sales seeding completed successfully")]
        public static partial void SalesSeedingCompleted(ILogger logger);

        [LoggerMessage(
            Level = LogLevel.Error,
            Message = "Error seeding sales")]
        public static partial void ErrorSeedingSales(ILogger logger, Exception exception);

        // Publishers Seeding
        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Seeding publishers...")]
        public static partial void SeedingPublishers(ILogger logger);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Seeded {Count} publishers")]
        public static partial void seededPublishers(ILogger logger, int count);

        // Authors Seeding
        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Seeding authors...")]
        public static partial void SeedingAuthors(ILogger logger);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Seeded {Count} authors")]
        public static partial void SeededAuthors(ILogger logger, int count);

        // Categories Seeding
        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Seeding categories...")]
        public static partial void SeedingCategories(ILogger logger);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Seeded {Count} categories")]
        public static partial void SeededCategories(ILogger logger, int count);

        // Books Seeding
        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Seeding books...")]
        public static partial void SeedingBooks(ILogger logger);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Seeded {Count} books")]
        public static partial void SeededBooks(ILogger logger, int count);

        [LoggerMessage(
            Level = LogLevel.Error,
            Message = "Failed to generate cover for {BookTitle}")]
        public static partial void FailedToGenerateCover(ILogger logger, Exception exception, string bookTitle);
    }
}
