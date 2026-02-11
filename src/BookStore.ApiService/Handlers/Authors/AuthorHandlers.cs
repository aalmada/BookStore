using BookStore.ApiService.Aggregates;
using BookStore.ApiService.Commands;
using BookStore.ApiService.Events;
using BookStore.ApiService.Infrastructure;
using BookStore.ApiService.Infrastructure.Extensions;
using BookStore.ApiService.Infrastructure.Logging;
using BookStore.Shared.Models;
using Marten;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;

namespace BookStore.ApiService.Handlers.Authors;

public static class AuthorHandlers
{
    public static async Task<IResult> Handle(
        CreateAuthor command,
        IDocumentSession session,
        IOptions<LocalizationOptions> localizationOptions,
        HybridCache cache,
        ILogger logger)
    {
        Log.Authors.AuthorCreating(logger, command.Id, command.Name, session.CorrelationId ?? "none");

        // Validate language codes in biographies if provided
        if (command.Translations?.Count > 0)
        {
            if (!CultureValidator.ValidateTranslations(command.Translations, out var invalidCodes))
            {
                Log.Authors.InvalidTranslationCodes(logger, command.Id, string.Join(", ", invalidCodes));
                return Result.Failure(Error.Validation(ErrorCodes.Authors.TranslationLanguageInvalid, $"The following language codes are not valid: {string.Join(", ", invalidCodes)}")).ToProblemDetails();
            }
        }

        // Validate that default language translation is provided
        var defaultLanguage = localizationOptions.Value.DefaultCulture;
        if (command.Translations is null || !command.Translations.ContainsKey(defaultLanguage))
        {
            Log.Authors.MissingDefaultTranslation(logger, command.Id, defaultLanguage);
            return Result.Failure(Error.Validation(ErrorCodes.Authors.DefaultTranslationRequired, $"A biography translation for the default language '{defaultLanguage}' must be provided")).ToProblemDetails();
        }

        // Validate biography lengths
        foreach (var (languageCode, translation) in command.Translations)
        {
            if (translation.Biography.Length > AuthorAggregate.MaxBiographyLength)
            {
                Log.Authors.BiographyTooLong(logger, command.Id, languageCode, AuthorAggregate.MaxBiographyLength, translation.Biography.Length);
                return Result.Failure(Error.Validation(ErrorCodes.Authors.BiographyTooLong, $"Biography for language '{languageCode}' cannot exceed {AuthorAggregate.MaxBiographyLength} characters")).ToProblemDetails();
            }
        }

        // Convert DTOs to domain objects
        var biographies = command.Translations.ToDictionary(
            kvp => kvp.Key,
            kvp => new AuthorTranslation(kvp.Value.Biography));

        var eventResult = AuthorAggregate.CreateEvent(
            command.Id,
            command.Name,
            biographies);

        if (eventResult.IsFailure)
        {
            return eventResult.ToProblemDetails();
        }

        _ = session.Events.StartStream<AuthorAggregate>(command.Id, eventResult.Value);

        Log.Authors.AuthorCreated(logger, command.Id, command.Name);

        // Invalidate cache
        await cache.RemoveByTagAsync([CacheTags.AuthorList], default);

        var defaultBiography = command.Translations?[localizationOptions.Value.DefaultCulture].Biography;

        return Results.Created(
            $"/api/admin/authors/{command.Id}",
            new AuthorDto(command.Id, command.Name, defaultBiography));
    }

    public static async Task<IResult> Handle(
        UpdateAuthor command,
        IDocumentSession session,
        IOptions<LocalizationOptions> localizationOptions,
        HybridCache cache,
        CancellationToken cancellationToken)
    {
        // Validate language codes in biographies if provided
        if (command.Translations?.Count > 0)
        {
            if (!CultureValidator.ValidateTranslations(command.Translations, out var invalidCodes))
            {
                return Result.Failure(Error.Validation(ErrorCodes.Authors.TranslationLanguageInvalid, $"The following language codes are not valid: {string.Join(", ", invalidCodes)}")).ToProblemDetails();
            }
        }

        // Validate that default language translation is provided
        var defaultLanguage = localizationOptions.Value.DefaultCulture;
        if (command.Translations is null || !command.Translations.ContainsKey(defaultLanguage))
        {
            return Result.Failure(Error.Validation(ErrorCodes.Authors.DefaultTranslationRequired, $"A biography translation for the default language '{defaultLanguage}' must be provided")).ToProblemDetails();
        }

        // Validate biography lengths
        foreach (var (languageCode, translation) in command.Translations)
        {
            if (translation.Biography.Length > AuthorAggregate.MaxBiographyLength)
            {
                return Result.Failure(Error.Validation(ErrorCodes.Authors.BiographyTooLong, $"Biography for language '{languageCode}' cannot exceed {AuthorAggregate.MaxBiographyLength} characters")).ToProblemDetails();
            }
        }

        var aggregate = await session.Events.AggregateStreamAsync<AuthorAggregate>(command.Id);
        if (aggregate is null)
        {
            return Result.Failure(Error.NotFound(ErrorCodes.Authors.NotDeleted, "Author not found")).ToProblemDetails();
        }

        var expectedVersion = ETagHelper.ParseETag(command.ETag);

        if (expectedVersion.HasValue && aggregate.Version != expectedVersion.Value)
        {
            return ETagHelper.PreconditionFailed();
        }

        // Convert DTOs to domain objects
        var biographies = command.Translations.ToDictionary(
            kvp => kvp.Key,
            kvp => new AuthorTranslation(kvp.Value.Biography));

        var eventResult = aggregate.UpdateEvent(command.Name, biographies);
        if (eventResult.IsFailure)
        {
            return eventResult.ToProblemDetails();
        }

        _ = session.Events.Append(command.Id, eventResult.Value);

        // Invalidate cache
        await cache.RemoveByTagAsync([CacheTags.AuthorList, CacheTags.ForItem(CacheTags.AuthorItemPrefix, command.Id)], cancellationToken);

        return Results.NoContent();
    }

    public static async Task<IResult> Handle(
        SoftDeleteAuthor command,
        IDocumentSession session,
        ILogger logger)
    {
        Log.Authors.AuthorSoftDeleting(logger, command.Id);

        var aggregate = await session.Events.AggregateStreamAsync<AuthorAggregate>(command.Id);
        if (aggregate is null)
        {
            Log.Authors.AuthorNotFound(logger, command.Id);
            return Result.Failure(Error.NotFound(ErrorCodes.Authors.NotDeleted, "Author not found")).ToProblemDetails();
        }

        var expectedVersion = ETagHelper.ParseETag(command.ETag);

        if (expectedVersion.HasValue && aggregate.Version != expectedVersion.Value)
        {
            return ETagHelper.PreconditionFailed();
        }

        var eventResult = aggregate.SoftDeleteEvent();
        if (eventResult.IsFailure)
        {
            return eventResult.ToProblemDetails();
        }

        _ = session.Events.Append(command.Id, eventResult.Value);

        return Results.NoContent();
    }

    public static async Task<IResult> Handle(
        RestoreAuthor command,
        IDocumentSession session,
        ILogger logger)
    {
        Log.Authors.AuthorRestoring(logger, command.Id);

        var aggregate = await session.Events.AggregateStreamAsync<AuthorAggregate>(command.Id);
        if (aggregate is null)
        {
            Log.Authors.AuthorNotFound(logger, command.Id);
            return Result.Failure(Error.NotFound(ErrorCodes.Authors.NotDeleted, "Author not found")).ToProblemDetails();
        }

        var expectedVersion = ETagHelper.ParseETag(command.ETag);

        if (expectedVersion.HasValue && aggregate.Version != expectedVersion.Value)
        {
            return ETagHelper.PreconditionFailed();
        }

        var eventResult = aggregate.RestoreEvent();
        if (eventResult.IsFailure)
        {
            return eventResult.ToProblemDetails();
        }

        _ = session.Events.Append(command.Id, eventResult.Value);

        return Results.NoContent();
    }
}
