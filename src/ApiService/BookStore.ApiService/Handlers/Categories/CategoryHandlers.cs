using BookStore.ApiService.Aggregates;
using BookStore.ApiService.Commands;
using BookStore.ApiService.Events;
using BookStore.ApiService.Infrastructure;
using BookStore.ApiService.Infrastructure.Logging;
using Marten;

namespace BookStore.ApiService.Handlers.Categories;

public static class CategoryHandlers
{
    public static IResult Handle(CreateCategory command, IDocumentSession session, ILogger logger)
    {
        Log.Categories.CategoryCreating(logger, command.Id, session.CorrelationId ?? "none");
        // Validate language codes in CategoryTranslation
        if (!CultureValidator.ValidateTranslations(command.Translations, out var invalidCodes))
        {
            Log.Categories.InvalidTranslationCodes(logger, command.Id, string.Join(", ", invalidCodes));
            return Results.BadRequest(new
            {
                error = "Invalid language codes in CategoryTranslation",
                invalidCodes,
                message = $"The following language codes are not valid: {string.Join(", ", invalidCodes)}"
            });
        }

        // Validate name and description lengths
        foreach (var (languageCode, translation) in command.Translations)
        {
            if (translation.Name.Length > CategoryAggregate.MaxNameLength)
            {
                Log.Categories.NameTooLong(logger, command.Id, languageCode, CategoryAggregate.MaxNameLength, translation.Name.Length);
                return Results.BadRequest(new
                {
                    error = "Category name too long",
                    languageCode,
                    maxLength = CategoryAggregate.MaxNameLength,
                    actualLength = translation.Name.Length,
                    message = $"Category name for language '{languageCode}' cannot exceed {CategoryAggregate.MaxNameLength} characters"
                });
            }

            if (translation.Description?.Length > CategoryAggregate.MaxDescriptionLength)
            {
                Log.Categories.DescriptionTooLong(logger, command.Id, languageCode, CategoryAggregate.MaxDescriptionLength, translation.Description.Length);
                return Results.BadRequest(new
                {
                    error = "Category description too long",
                    languageCode,
                    maxLength = CategoryAggregate.MaxDescriptionLength,
                    actualLength = translation.Description.Length,
                    message = $"Category description for language '{languageCode}' cannot exceed {CategoryAggregate.MaxDescriptionLength} characters"
                });
            }
        }

        // Convert DTOs to domain objects
        var translations = command.Translations.ToDictionary(
            kvp => kvp.Key,
            kvp => new CategoryTranslation(kvp.Value.Name, kvp.Value.Description));

        var @event = CategoryAggregate.Create(
            command.Id,
            translations);

        _ = session.Events.StartStream<CategoryAggregate>(command.Id, @event);

        Log.Categories.CategoryCreated(logger, command.Id);

        return Results.Created(
            $"/api/admin/categories/{command.Id}",
            new { id = command.Id, correlationId = session.CorrelationId });
    }

    public static async Task<IResult> Handle(
        UpdateCategory command,
        IDocumentSession session,
        HttpContext context,
        ILogger logger)
    {
        // Validate language codes in CategoryTranslation
        if (!CultureValidator.ValidateTranslations(command.Translations, out var invalidCodes))
        {
            return Results.BadRequest(new
            {
                error = "Invalid language codes in CategoryTranslation",
                invalidCodes,
                message = $"The following language codes are not valid: {string.Join(", ", invalidCodes)}"
            });
        }

        // Validate name and description lengths
        foreach (var (languageCode, translation) in command.Translations)
        {
            if (translation.Name.Length > CategoryAggregate.MaxNameLength)
            {
                return Results.BadRequest(new
                {
                    error = "Category name too long",
                    languageCode,
                    maxLength = CategoryAggregate.MaxNameLength,
                    actualLength = translation.Name.Length,
                    message = $"Category name for language '{languageCode}' cannot exceed {CategoryAggregate.MaxNameLength} characters"
                });
            }

            if (translation.Description?.Length > CategoryAggregate.MaxDescriptionLength)
            {
                return Results.BadRequest(new
                {
                    error = "Category description too long",
                    languageCode,
                    maxLength = CategoryAggregate.MaxDescriptionLength,
                    actualLength = translation.Description.Length,
                    message = $"Category description for language '{languageCode}' cannot exceed {CategoryAggregate.MaxDescriptionLength} characters"
                });
            }
        }

        var streamState = await session.Events.FetchStreamStateAsync(command.Id);
        if (streamState is null)
        {
            Log.Categories.CategoryNotFound(logger, command.Id);
            return Results.NotFound();
        }

        var currentETag = ETagHelper.GenerateETag(streamState.Version);
        if (!string.IsNullOrEmpty(command.ETag) &&
            !ETagHelper.CheckIfMatch(context, currentETag))
        {
            Log.Categories.ETagMismatch(logger, command.Id, currentETag, command.ETag);
            return ETagHelper.PreconditionFailed();
        }

        var aggregate = await session.Events.AggregateStreamAsync<CategoryAggregate>(command.Id);
        if (aggregate is null)
        {
            Log.Categories.CategoryNotFound(logger, command.Id);
            return Results.NotFound();
        }

        Log.Categories.CategoryUpdating(logger, command.Id, streamState.Version);

        var translations = command.Translations.ToDictionary(
            kvp => kvp.Key,
            kvp => new CategoryTranslation(kvp.Value.Name, kvp.Value.Description));

        var @event = aggregate.Update(translations);
        _ = session.Events.Append(command.Id, @event);

        Log.Categories.CategoryUpdated(logger, command.Id);

        var newStreamState = await session.Events.FetchStreamStateAsync(command.Id);
        var newETag = ETagHelper.GenerateETag(newStreamState!.Version);
        ETagHelper.AddETagHeader(context, newETag);

        return Results.NoContent();
    }

    public static async Task<IResult> Handle(
        SoftDeleteCategory command,
        IDocumentSession session,
        HttpContext context,
        ILogger logger)
    {
        Log.Categories.CategorySoftDeleting(logger, command.Id);

        var streamState = await session.Events.FetchStreamStateAsync(command.Id);
        if (streamState is null)
        {
            Log.Categories.CategoryNotFound(logger, command.Id);
            return Results.NotFound();
        }

        var currentETag = ETagHelper.GenerateETag(streamState.Version);
        if (!string.IsNullOrEmpty(command.ETag) &&
            !ETagHelper.CheckIfMatch(context, currentETag))
        {
            return ETagHelper.PreconditionFailed();
        }

        var aggregate = await session.Events.AggregateStreamAsync<CategoryAggregate>(command.Id);
        if (aggregate is null)
        {
            Log.Categories.CategoryNotFound(logger, command.Id);
            return Results.NotFound();
        }

        var @event = aggregate.SoftDelete();
        _ = session.Events.Append(command.Id, @event);

        Log.Categories.CategorySoftDeleted(logger, command.Id);

        var newStreamState = await session.Events.FetchStreamStateAsync(command.Id);
        var newETag = ETagHelper.GenerateETag(newStreamState!.Version);
        ETagHelper.AddETagHeader(context, newETag);

        return Results.NoContent();
    }

    public static async Task<IResult> Handle(
        RestoreCategory command,
        IDocumentSession session,
        HttpContext context,
        ILogger logger)
    {
        Log.Categories.CategoryRestoring(logger, command.Id);

        var streamState = await session.Events.FetchStreamStateAsync(command.Id);
        if (streamState is null)
        {
            Log.Categories.CategoryNotFound(logger, command.Id);
            return Results.NotFound();
        }

        var currentETag = ETagHelper.GenerateETag(streamState.Version);
        if (!string.IsNullOrEmpty(command.ETag) &&
            !ETagHelper.CheckIfMatch(context, currentETag))
        {
            return ETagHelper.PreconditionFailed();
        }

        var aggregate = await session.Events.AggregateStreamAsync<CategoryAggregate>(command.Id);
        if (aggregate is null)
        {
            Log.Categories.CategoryNotFound(logger, command.Id);
            return Results.NotFound();
        }

        var @event = aggregate.Restore();
        _ = session.Events.Append(command.Id, @event);

        Log.Categories.CategoryRestored(logger, command.Id);

        var newStreamState = await session.Events.FetchStreamStateAsync(command.Id);
        var newETag = ETagHelper.GenerateETag(newStreamState!.Version);
        ETagHelper.AddETagHeader(context, newETag);

        return Results.NoContent();
    }
}
