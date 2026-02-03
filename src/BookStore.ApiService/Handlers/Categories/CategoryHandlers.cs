using BookStore.ApiService.Aggregates;
using BookStore.ApiService.Commands;
using BookStore.ApiService.Events;
using BookStore.ApiService.Infrastructure;
using BookStore.ApiService.Infrastructure.Extensions;
using BookStore.ApiService.Infrastructure.Logging;
using BookStore.Shared.Notifications;
using Marten;
using Microsoft.Extensions.Caching.Hybrid;

namespace BookStore.ApiService.Handlers.Categories;

public static class CategoryHandlers
{
    public static async Task<IResult> Handle(
        CreateCategory command,
        IDocumentSession session,
        HybridCache cache,
        ILogger<CreateCategory> logger)
    {
        Log.Categories.CategoryCreating(logger, command.Id, session.CorrelationId ?? "none");
        // Validate language codes in CategoryTranslation
        if (!CultureValidator.ValidateTranslations(command.Translations, out var invalidCodes))
        {
            Log.Categories.InvalidTranslationCodes(logger, command.Id, string.Join(", ", invalidCodes));
            return Result.Failure(Error.Validation(ErrorCodes.Categories.TranslationLanguageInvalid, $"The following language codes are not valid: {string.Join(", ", invalidCodes)}")).ToProblemDetails();
        }

        // Validate name lengths
        foreach (var (languageCode, translation) in command.Translations)
        {
            if (string.IsNullOrWhiteSpace(translation.Name))
            {
                Log.Categories.InvalidTranslationCodes(logger, command.Id, languageCode);
                return Result.Failure(Error.Validation(ErrorCodes.Categories.NameRequired, $"Category name for language '{languageCode}' cannot be empty")).ToProblemDetails();
            }

            if (translation.Name.Length > CategoryAggregate.MaxNameLength)
            {
                Log.Categories.NameTooLong(logger, command.Id, languageCode, CategoryAggregate.MaxNameLength, translation.Name.Length);
                return Result.Failure(Error.Validation(ErrorCodes.Categories.NameTooLong, $"Category name for language '{languageCode}' cannot exceed {CategoryAggregate.MaxNameLength} characters")).ToProblemDetails();
            }
        }

        // Convert DTOs to domain objects
        var translations = command.Translations.ToDictionary(
            kvp => kvp.Key,
            kvp => new CategoryTranslation(kvp.Value.Name));

        var eventResult = CategoryAggregate.CreateEvent(
            command.Id,
            translations);

        if (eventResult.IsFailure)
        {
            return eventResult.ToProblemDetails();
        }

        _ = session.Events.StartStream<CategoryAggregate>(command.Id, eventResult.Value);

        // Invalidate cache
        await cache.RemoveByTagAsync([CacheTags.CategoryList], default);

        return Results.Created(
            $"/api/admin/categories/{command.Id}",
            new { id = command.Id, correlationId = session.CorrelationId });
    }

    public static async Task<IResult> Handle(
        UpdateCategory command,
        IDocumentSession session,
        IHttpContextAccessor httpContextAccessor,
        HybridCache cache,
        ILogger<UpdateCategory> logger)
    {
        // Validate language codes in CategoryTranslation
        if (!CultureValidator.ValidateTranslations(command.Translations, out var invalidCodes))
        {
            return Result.Failure(Error.Validation(ErrorCodes.Categories.TranslationLanguageInvalid, $"The following language codes are not valid: {string.Join(", ", invalidCodes)}")).ToProblemDetails();
        }

        // Validate name lengths
        foreach (var (languageCode, translation) in command.Translations)
        {
            if (string.IsNullOrWhiteSpace(translation.Name))
            {
                return Result.Failure(Error.Validation(ErrorCodes.Categories.NameRequired, $"Category name for language '{languageCode}' cannot be empty")).ToProblemDetails();
            }

            if (translation.Name.Length > CategoryAggregate.MaxNameLength)
            {
                return Result.Failure(Error.Validation(ErrorCodes.Categories.NameTooLong, $"Category name for language '{languageCode}' cannot exceed {CategoryAggregate.MaxNameLength} characters")).ToProblemDetails();
            }
        }

        var streamState = await session.Events.FetchStreamStateAsync(command.Id);
        if (streamState is null)
        {
            Log.Categories.CategoryNotFound(logger, command.Id);
            return Result.Failure(Error.NotFound(ErrorCodes.Categories.NotDeleted, "Category not found")).ToProblemDetails();
        }

        var context = httpContextAccessor.HttpContext;
        var currentETag = ETagHelper.GenerateETag(streamState.Version);
        if (context != null && !string.IsNullOrEmpty(command.ETag) &&
            !ETagHelper.CheckIfMatch(context, currentETag))
        {
            Log.Categories.ETagMismatch(logger, command.Id, currentETag, command.ETag);
            return ETagHelper.PreconditionFailed();
        }

        var aggregate = await session.Events.AggregateStreamAsync<CategoryAggregate>(command.Id);
        if (aggregate is null)
        {
            Log.Categories.CategoryNotFound(logger, command.Id);
            return Result.Failure(Error.NotFound(ErrorCodes.Categories.NotDeleted, "Category not found")).ToProblemDetails();
        }

        Log.Categories.CategoryUpdating(logger, command.Id, streamState.Version);

        var translations = command.Translations.ToDictionary(
            kvp => kvp.Key,
            kvp => new CategoryTranslation(kvp.Value.Name));

        var eventResult = aggregate.UpdateEvent(translations);
        if (eventResult.IsFailure)
        {
            return eventResult.ToProblemDetails();
        }

        _ = session.Events.Append(command.Id, eventResult.Value);

        // Invalidate cache
        await cache.RemoveByTagAsync([CacheTags.CategoryList, CacheTags.ForItem(CacheTags.CategoryItemPrefix, command.Id)], context?.RequestAborted ?? default);

        return Results.NoContent();
    }

    public static async Task<IResult> Handle(
        SoftDeleteCategory command,
        IDocumentSession session,
        IHttpContextAccessor httpContextAccessor,
        HybridCache cache,
        ILogger<SoftDeleteCategory> logger)
    {
        Log.Categories.CategorySoftDeleting(logger, command.Id);

        var streamState = await session.Events.FetchStreamStateAsync(command.Id);
        if (streamState is null)
        {
            Log.Categories.CategoryNotFound(logger, command.Id);
            return Result.Failure(Error.NotFound(ErrorCodes.Categories.NotDeleted, "Category not found")).ToProblemDetails();
        }

        var context = httpContextAccessor.HttpContext;
        var currentETag = ETagHelper.GenerateETag(streamState.Version);
        if (context != null && !string.IsNullOrEmpty(command.ETag) &&
            !ETagHelper.CheckIfMatch(context, currentETag))
        {
            return ETagHelper.PreconditionFailed();
        }

        var aggregate = await session.Events.AggregateStreamAsync<CategoryAggregate>(command.Id);
        if (aggregate is null)
        {
            Log.Categories.CategoryNotFound(logger, command.Id);
            return Result.Failure(Error.NotFound(ErrorCodes.Categories.NotDeleted, "Category not found")).ToProblemDetails();
        }

        var eventResult = aggregate.SoftDeleteEvent();
        if (eventResult.IsFailure)
        {
            return eventResult.ToProblemDetails();
        }

        _ = session.Events.Append(command.Id, eventResult.Value);

        // Invalidate cache
        await cache.RemoveByTagAsync([CacheTags.CategoryList, CacheTags.ForItem(CacheTags.CategoryItemPrefix, command.Id)], context?.RequestAborted ?? default);

        return Results.NoContent();
    }

    public static async Task<IResult> Handle(
        RestoreCategory command,
        IDocumentSession session,
        IHttpContextAccessor httpContextAccessor,
        HybridCache cache,
        ILogger<RestoreCategory> logger)
    {
        Log.Categories.CategoryRestoring(logger, command.Id);

        var streamState = await session.Events.FetchStreamStateAsync(command.Id);
        if (streamState is null)
        {
            Log.Categories.CategoryNotFound(logger, command.Id);
            return Result.Failure(Error.NotFound(ErrorCodes.Categories.NotDeleted, "Category not found")).ToProblemDetails();
        }

        var context = httpContextAccessor.HttpContext;
        var currentETag = ETagHelper.GenerateETag(streamState.Version);
        if (context != null && !string.IsNullOrEmpty(command.ETag) &&
            !ETagHelper.CheckIfMatch(context, currentETag))
        {
            return ETagHelper.PreconditionFailed();
        }

        var aggregate = await session.Events.AggregateStreamAsync<CategoryAggregate>(command.Id);
        if (aggregate is null)
        {
            Log.Categories.CategoryNotFound(logger, command.Id);
            return Result.Failure(Error.NotFound(ErrorCodes.Categories.NotDeleted, "Category not found")).ToProblemDetails();
        }

        var eventResult = aggregate.RestoreEvent();
        if (eventResult.IsFailure)
        {
            return eventResult.ToProblemDetails();
        }

        _ = session.Events.Append(command.Id, eventResult.Value);

        // Invalidate cache
        await cache.RemoveByTagAsync([CacheTags.CategoryList, CacheTags.ForItem(CacheTags.CategoryItemPrefix, command.Id)], context?.RequestAborted ?? default);

        return Results.NoContent();
    }
}
