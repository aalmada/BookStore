using BookStore.ApiService.Aggregates;
using BookStore.ApiService.Commands;
using BookStore.ApiService.Events;
using BookStore.ApiService.Infrastructure;
using BookStore.ApiService.Infrastructure.Logging;
using BookStore.Shared.Models;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace BookStore.ApiService.Handlers.Books;

/// <summary>
/// Wolverine handlers for Book commands
/// Handlers are auto-discovered by Wolverine and provide clean separation of concerns
/// </summary>
public static class BookHandlers
{
    /// <summary>
    /// Handle CreateBook command
    /// Wolverine automatically manages the Marten session and commits the transaction
    /// Returns a notification that will be published to SignalR
    /// </summary>
    public static (IResult, BookStore.ApiService.Events.Notifications.BookCreatedNotification) Handle(
        CreateBook command,
        IDocumentSession session,
        IOptions<LocalizationOptions> localizationOptions,
        ILogger logger)
    {
        Log.Books.BookCreating(logger, command.Id, command.Title, session.CorrelationId ?? "none");

        // Validate language code
        if (!CultureValidator.IsValidCultureCode(command.Language))
        {
            Log.Books.InvalidLanguageCode(logger, command.Id, command.Language);
            return (Results.BadRequest(new
            {
                error = "Invalid language code",
                languageCode = command.Language,
                message = $"The language code '{command.Language}' is not valid. Must be a valid ISO 639-1 (e.g., 'en'), ISO 639-3 (e.g., 'fil'), or culture code (e.g., 'en-US')"
            }), null!);
        }

        // Validate language codes in descriptions if provided
        if (command.Translations?.Count > 0)
        {
            if (!CultureValidator.ValidateTranslations(command.Translations, out var invalidCodes))
            {
                Log.Books.InvalidTranslationCodes(logger, command.Id, string.Join(", ", invalidCodes));
                return (Results.BadRequest(new
                {
                    error = "Invalid language codes in descriptions",
                    invalidCodes,
                    message = $"The following language codes are not valid: {string.Join(", ", invalidCodes)}"
                }), null!);
            }
        }

        // Validate that default language translation is provided
        var defaultLanguage = localizationOptions.Value.DefaultCulture;
        if (command.Translations is null || !command.Translations.ContainsKey(defaultLanguage))
        {
            Log.Books.MissingDefaultTranslation(logger, command.Id, defaultLanguage);
            return (Results.BadRequest(new
            {
                error = "Default language translation required",
                message = $"A description translation for the default language '{defaultLanguage}' must be provided"
            }), null!);
        }

        // Validate description lengths
        foreach (var (languageCode, translation) in command.Translations)
        {
            if (translation.Description.Length > BookAggregate.MaxDescriptionLength)
            {
                Log.Books.DescriptionTooLong(logger, command.Id, languageCode, BookAggregate.MaxDescriptionLength, translation.Description.Length);
                return (Results.BadRequest(new
                {
                    error = "Description too long",
                    languageCode,
                    maxLength = BookAggregate.MaxDescriptionLength,
                    actualLength = translation.Description.Length,
                    message = $"Description for language '{languageCode}' cannot exceed {BookAggregate.MaxDescriptionLength} characters"
                }), null!);
            }
        }

        // Convert DTOs to domain objects
        var descriptions = command.Translations.ToDictionary(
            kvp => kvp.Key,
            kvp => new BookTranslation(kvp.Value.Description));

        try
        {
            var @event = BookAggregate.Create(
                command.Id,
                command.Title,
                command.Isbn,
                command.Language,
                descriptions,
                command.PublicationDate,
                command.PublisherId,
                [.. command.AuthorIds],
                [.. command.CategoryIds]);

            _ = session.Events.StartStream<BookAggregate>(command.Id, @event);
        }
        catch (ArgumentException ex)
        {
            Log.Books.InvalidBookData(logger, command.Id, ex.Message);
            return (Results.BadRequest(new { error = ex.Message }), null!);
        }
        catch (InvalidOperationException ex)
        {
            Log.Books.InvalidBookOperation(logger, command.Id, ex.Message);
            return (Results.BadRequest(new { error = ex.Message }), null!);
        }

        Log.Books.BookCreated(logger, command.Id, command.Title);

        // Create notification for SignalR (will be published as cascading message)
        var notification = new BookStore.ApiService.Events.Notifications.BookCreatedNotification(
            command.Id,
            command.Title,
            DateTimeOffset.UtcNow);

        // Wolverine automatically calls SaveChangesAsync and publishes the notification
        return (Results.Created(
            $"/api/admin/books/{command.Id}",
            new { id = command.Id, correlationId = session.CorrelationId }), notification);
    }

    /// <summary>
    /// Handle UpdateBook command with ETag validation
    /// </summary>
    public static async Task<IResult> Handle(
        UpdateBook command,
        IDocumentSession session,
        IHttpContextAccessor contextAccessor,
        IOptions<LocalizationOptions> localizationOptions,
        ILogger logger)
    {
        var context = contextAccessor.HttpContext!;
        // Validate language code
        if (!CultureValidator.IsValidCultureCode(command.Language))
        {
            return Results.BadRequest(new
            {
                error = "Invalid language code",
                languageCode = command.Language,
                message = $"The language code '{command.Language}' is not valid. Must be a valid ISO 639-1 (e.g., 'en'), ISO 639-3 (e.g., 'fil'), or culture code (e.g., 'en-US')"
            });
        }

        // Validate language codes in descriptions if provided
        if (command.Translations?.Count > 0)
        {
            if (!CultureValidator.ValidateTranslations(command.Translations, out var invalidCodes))
            {
                return Results.BadRequest(new
                {
                    error = "Invalid language codes in descriptions",
                    invalidCodes,
                    message = $"The following language codes are not valid: {string.Join(", ", invalidCodes)}"
                });
            }
        }

        // Validate that default language translation is provided
        var defaultLanguage = localizationOptions.Value.DefaultCulture;
        if (command.Translations is null || !command.Translations.ContainsKey(defaultLanguage))
        {
            return Results.BadRequest(new
            {
                error = "Default language translation required",
                message = $"A description translation for the default language '{defaultLanguage}' must be provided"
            });
        }

        // Validate description lengths
        foreach (var (languageCode, translation) in command.Translations)
        {
            if (translation.Description.Length > BookAggregate.MaxDescriptionLength)
            {
                return Results.BadRequest(new
                {
                    error = "Description too long",
                    languageCode,
                    maxLength = BookAggregate.MaxDescriptionLength,
                    actualLength = translation.Description.Length,
                    message = $"Description for language '{languageCode}' cannot exceed {BookAggregate.MaxDescriptionLength} characters"
                });
            }
        }

        // Get current stream state for ETag validation
        var streamState = await session.Events.FetchStreamStateAsync(command.Id);
        if (streamState is null)
        {
            Log.Books.BookNotFound(logger, command.Id);
            return Results.NotFound();
        }

        var currentETag = ETagHelper.GenerateETag(streamState.Version);

        // Check If-Match header for optimistic concurrency
        if (!string.IsNullOrEmpty(command.ETag) &&
            !ETagHelper.CheckIfMatch(context, currentETag))
        {
            Log.Books.ETagMismatch(logger, command.Id, currentETag, command.ETag);
            return ETagHelper.PreconditionFailed();
        }

        var aggregate = await session.Events.AggregateStreamAsync<BookAggregate>(command.Id);
        if (aggregate is null)
        {
            Log.Books.BookNotFound(logger, command.Id);
            return Results.NotFound();
        }

        Log.Books.BookUpdating(logger, command.Id, command.Title, streamState.Version);

        // Convert DTOs to domain objects
        var descriptions = command.Translations.ToDictionary(
            kvp => kvp.Key,
            kvp => new BookTranslation(kvp.Value.Description));

        try
        {
            var @event = aggregate.Update(
                command.Title,
                command.Isbn,
                command.Language,
                descriptions,
                command.PublicationDate,
                command.PublisherId,
                [.. command.AuthorIds],
                [.. command.CategoryIds]);

            _ = session.Events.Append(command.Id, @event);
        }
        catch (ArgumentException ex)
        {
            Log.Books.InvalidBookData(logger, command.Id, ex.Message);
            return Results.BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            Log.Books.InvalidBookOperation(logger, command.Id, ex.Message);
            return Results.BadRequest(new { error = ex.Message });
        }

        Log.Books.BookUpdated(logger, command.Id);

        // Get new stream state and return new ETag
        var newStreamState = await session.Events.FetchStreamStateAsync(command.Id);
        var newETag = ETagHelper.GenerateETag(newStreamState!.Version);
        ETagHelper.AddETagHeader(context, newETag);

        return Results.NoContent();
    }

    /// <summary>
    /// Handle SoftDeleteBook command with ETag validation
    /// </summary>
    public static async Task<IResult> Handle(
        SoftDeleteBook command,
        IDocumentSession session,
        IHttpContextAccessor contextAccessor,
        ILogger logger)
    {
        var context = contextAccessor.HttpContext!;
        Log.Books.BookSoftDeleting(logger, command.Id);

        // Get current stream state for ETag validation
        var streamState = await session.Events.FetchStreamStateAsync(command.Id);
        if (streamState is null)
        {
            Log.Books.BookNotFound(logger, command.Id);
            return Results.NotFound();
        }

        var currentETag = ETagHelper.GenerateETag(streamState.Version);

        // Check If-Match header for optimistic concurrency
        if (!string.IsNullOrEmpty(command.ETag) &&
            !ETagHelper.CheckIfMatch(context, currentETag))
        {
            return ETagHelper.PreconditionFailed();
        }

        var aggregate = await session.Events.AggregateStreamAsync<BookAggregate>(command.Id);
        if (aggregate is null)
        {
            Log.Books.BookNotFound(logger, command.Id);
            return Results.NotFound();
        }

        try
        {
            var @event = aggregate.SoftDelete();
            _ = session.Events.Append(command.Id, @event);
        }
        catch (InvalidOperationException ex)
        {
            Log.Books.InvalidBookOperation(logger, command.Id, ex.Message);
            return Results.BadRequest(new { error = ex.Message });
        }

        Log.Books.BookSoftDeleted(logger, command.Id);

        // Get new stream state and return new ETag
        var newStreamState = await session.Events.FetchStreamStateAsync(command.Id);
        var newETag = ETagHelper.GenerateETag(newStreamState!.Version);
        ETagHelper.AddETagHeader(context, newETag);

        return Results.NoContent();
    }

    /// <summary>
    /// Handle RestoreBook command with ETag validation
    /// </summary>
    public static async Task<IResult> Handle(
        RestoreBook command,
        IDocumentSession session,
        IHttpContextAccessor contextAccessor,
        ILogger logger)
    {
        var context = contextAccessor.HttpContext!;
        Log.Books.BookRestoring(logger, command.Id);

        // Get current stream state for ETag validation
        var streamState = await session.Events.FetchStreamStateAsync(command.Id);
        if (streamState is null)
        {
            Log.Books.BookNotFound(logger, command.Id);
            return Results.NotFound();
        }

        var currentETag = ETagHelper.GenerateETag(streamState.Version);

        // Check If-Match header for optimistic concurrency
        if (!string.IsNullOrEmpty(command.ETag) &&
            !ETagHelper.CheckIfMatch(context, currentETag))
        {
            return ETagHelper.PreconditionFailed();
        }

        var aggregate = await session.Events.AggregateStreamAsync<BookAggregate>(command.Id);
        if (aggregate is null)
        {
            Log.Books.BookNotFound(logger, command.Id);
            return Results.NotFound();
        }

        try
        {
            var @event = aggregate.Restore();
            _ = session.Events.Append(command.Id, @event);
        }
        catch (InvalidOperationException ex)
        {
            Log.Books.InvalidBookOperation(logger, command.Id, ex.Message);
            return Results.BadRequest(new { error = ex.Message });
        }

        Log.Books.BookRestored(logger, command.Id);

        // Get new stream state and return new ETag
        var newStreamState = await session.Events.FetchStreamStateAsync(command.Id);
        var newETag = ETagHelper.GenerateETag(newStreamState!.Version);
        ETagHelper.AddETagHeader(context, newETag);

        return Results.NoContent();
    }
}
