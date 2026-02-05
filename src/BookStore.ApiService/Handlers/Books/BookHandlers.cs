using BookStore.ApiService.Aggregates;
using BookStore.ApiService.Commands;
using BookStore.ApiService.Events;
using BookStore.ApiService.Infrastructure;
using BookStore.ApiService.Infrastructure.Extensions;
using BookStore.ApiService.Infrastructure.Logging;
using BookStore.Shared.Models;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Caching.Hybrid;
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
    public static async Task<IResult> Handle(
        CreateBook command,
        IDocumentSession session,
        IOptions<LocalizationOptions> localizationOptions,
        IOptions<CurrencyOptions> currencyOptions,
        HybridCache cache,
        ILogger logger)
    {
        Log.Books.BookCreating(logger, command.Id, command.Title, session.CorrelationId ?? "none");

        // Validate language code
        if (!CultureValidator.IsValidCultureCode(command.Language))
        {
            Log.Books.InvalidLanguageCode(logger, command.Id, command.Language);
            return Result.Failure(Error.Validation(ErrorCodes.Books.LanguageInvalid, "Invalid language code")).ToProblemDetails();
        }

        // Validate language codes in descriptions if provided
        if (command.Translations?.Count > 0)
        {
            if (!CultureValidator.ValidateTranslations(command.Translations, out var invalidCodes))
            {
                Log.Books.InvalidTranslationCodes(logger, command.Id, string.Join(", ", invalidCodes));
                return Result.Failure(Error.Validation(ErrorCodes.Books.TranslationLanguageInvalid, "Invalid language codes in descriptions")).ToProblemDetails();
            }
        }

        // Validate that default language translation is provided
        var defaultLanguage = localizationOptions.Value.DefaultCulture;
        if (command.Translations is null || !command.Translations.ContainsKey(defaultLanguage))
        {
            Log.Books.MissingDefaultTranslation(logger, command.Id, defaultLanguage);
            return Result.Failure(Error.Validation(ErrorCodes.Books.DefaultTranslationRequired, "Default language translation required")).ToProblemDetails();
        }

        // Validate description lengths
        foreach (var (languageCode, translation) in command.Translations)
        {
            if (translation.Description.Length > BookAggregate.MaxDescriptionLength)
            {
                Log.Books.DescriptionTooLong(logger, command.Id, languageCode, BookAggregate.MaxDescriptionLength, translation.Description.Length);
                return Result.Failure(Error.Validation(ErrorCodes.Books.DescriptionTooLong, "Description too long")).ToProblemDetails();
            }
        }

        // Validate that default currency price is provided
        var defaultCurrency = currencyOptions.Value.DefaultCurrency;
        if (command.Prices is null || !command.Prices.ContainsKey(defaultCurrency))
        {
            return Result.Failure(Error.Validation(ErrorCodes.Books.DefaultPriceRequired, "Default currency price required")).ToProblemDetails();
        }

        // Validate supported currencies
        var invalidCurrencies = command.Prices.Keys.Where(c => !currencyOptions.Value.SupportedCurrencies.Contains(c, StringComparer.OrdinalIgnoreCase)).ToList();
        if (invalidCurrencies.Count > 0)
        {
            return Result.Failure(Error.Validation(ErrorCodes.Books.PriceCurrencyInvalid, "Invalid currencies provided")).ToProblemDetails();
        }

        // Convert DTOs to domain objects
        var descriptions = command.Translations.ToDictionary(
            kvp => kvp.Key,
            kvp => new BookTranslation(kvp.Value.Description));

        var eventResult = BookAggregate.CreateEvent(
            command.Id,
            command.Title,
            command.Isbn,
            command.Language,
            descriptions,
            command.PublicationDate,
            command.PublisherId,
            [.. command.AuthorIds ?? []],
            [.. command.CategoryIds ?? []],
            command.Prices?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? []);

        if (eventResult.IsFailure)
        {
            Log.Books.InvalidBookData(logger, command.Id, eventResult.Error.Message);
            return eventResult.ToProblemDetails();
        }

        _ = session.Events.StartStream<BookAggregate>(command.Id, eventResult.Value);

        // Invalidate cache
        await cache.RemoveByTagAsync([CacheTags.BookList], default);

        return Results.Created(
            $"/api/admin/books/{command.Id}",
            new { id = command.Id, correlationId = session.CorrelationId });
    }

    /// <summary>
    /// Handle UpdateBook command with ETag validation
    /// </summary>
    public static async Task<IResult> Handle(
        UpdateBook command,
        IDocumentSession session,
        IHttpContextAccessor contextAccessor,
        IOptions<LocalizationOptions> localizationOptions,
        IOptions<CurrencyOptions> currencyOptions,
        HybridCache cache,
        ILogger logger)
    {
        var context = contextAccessor.HttpContext!;
        // Validate language code
        if (!CultureValidator.IsValidCultureCode(command.Language))
        {
            return Result.Failure(Error.Validation(ErrorCodes.Books.LanguageInvalid, "Invalid language code")).ToProblemDetails();
        }

        // Validate language codes in descriptions if provided
        if (command.Translations?.Count > 0)
        {
            if (!CultureValidator.ValidateTranslations(command.Translations, out var invalidCodes))
            {
                return Result.Failure(Error.Validation(ErrorCodes.Books.TranslationLanguageInvalid, "Invalid language codes in descriptions")).ToProblemDetails();
            }
        }

        // Validate that default language translation is provided
        var defaultLanguage = localizationOptions.Value.DefaultCulture;
        if (command.Translations is null || !command.Translations.ContainsKey(defaultLanguage))
        {
            return Result.Failure(Error.Validation(ErrorCodes.Books.DefaultTranslationRequired, "Default language translation required")).ToProblemDetails();
        }

        // Validate description lengths
        foreach (var (languageCode, translation) in command.Translations)
        {
            if (translation.Description.Length > BookAggregate.MaxDescriptionLength)
            {
                return Result.Failure(Error.Validation(ErrorCodes.Books.DescriptionTooLong, "Description too long")).ToProblemDetails();
            }
        }

        // Validate that default currency price is provided
        var defaultCurrency = currencyOptions.Value.DefaultCurrency;
        if (command.Prices is null || !command.Prices.ContainsKey(defaultCurrency))
        {
            return Result.Failure(Error.Validation(ErrorCodes.Books.DefaultPriceRequired, "Default currency price required")).ToProblemDetails();
        }

        // Validate supported currencies
        var invalidCurrencies = command.Prices.Keys.Where(c => !currencyOptions.Value.SupportedCurrencies.Contains(c, StringComparer.OrdinalIgnoreCase)).ToList();
        if (invalidCurrencies.Count > 0)
        {
            return Result.Failure(Error.Validation(ErrorCodes.Books.PriceCurrencyInvalid, "Invalid currencies provided")).ToProblemDetails();
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

        var eventResult = aggregate.UpdateEvent(
            command.Title,
            command.Isbn,
            command.Language,
            descriptions,
            command.PublicationDate,
            command.PublisherId,
            [.. command.AuthorIds],
            [.. command.CategoryIds],
            command.Prices?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? []);

        if (eventResult.IsFailure)
        {
            Log.Books.InvalidBookData(logger, command.Id, eventResult.Error.Message);
            return eventResult.ToProblemDetails();
        }

        _ = session.Events.Append(command.Id, eventResult.Value);

        return Results.NoContent();
    }

    /// <summary>
    /// Handle SoftDeleteBook command with ETag validation
    /// </summary>
    public static async Task<IResult> Handle(
        SoftDeleteBook command,
        IDocumentSession session,
        IHttpContextAccessor contextAccessor,
        HybridCache cache,
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

        var eventResult = aggregate.SoftDeleteEvent();
        if (eventResult.IsFailure)
        {
            Log.Books.InvalidBookOperation(logger, command.Id, eventResult.Error.Message);
            return eventResult.ToProblemDetails();
        }

        _ = session.Events.Append(command.Id, eventResult.Value);

        return Results.NoContent();
    }

    /// <summary>
    /// Handle RestoreBook command with ETag validation
    /// </summary>
    public static async Task<IResult> Handle(
        RestoreBook command,
        IDocumentSession session,
        IHttpContextAccessor contextAccessor,
        HybridCache cache,
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

        var eventResult = aggregate.RestoreEvent();
        if (eventResult.IsFailure)
        {
            Log.Books.InvalidBookOperation(logger, command.Id, eventResult.Error.Message);
            return eventResult.ToProblemDetails();
        }

        _ = session.Events.Append(command.Id, eventResult.Value);

        return Results.NoContent();
    }
}

