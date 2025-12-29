using BookStore.ApiService.Aggregates;
using BookStore.ApiService.Commands;
using BookStore.ApiService.Events;
using BookStore.ApiService.Infrastructure;
using Marten;

namespace BookStore.ApiService.Handlers.Categories;

public static class CategoryHandlers
{
    public static IResult Handle(CreateCategory command, IDocumentSession session)
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

        // Convert DTOs to domain objects
        var translations = command.Translations.ToDictionary(
            kvp => kvp.Key,
            kvp => new CategoryTranslation(kvp.Value.Name, kvp.Value.Description));

        var @event = CategoryAggregate.Create(
            command.Id,
            translations);

        _ = session.Events.StartStream<CategoryAggregate>(command.Id, @event);

        return Results.Created(
            $"/api/admin/categories/{command.Id}",
            new { id = command.Id, correlationId = session.CorrelationId });
    }

    public static async Task<IResult> Handle(
        UpdateCategory command,
        IDocumentSession session,
        HttpContext context)
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

        var streamState = await session.Events.FetchStreamStateAsync(command.Id);
        if (streamState == null)
        {
            return Results.NotFound();
        }

        var currentETag = ETagHelper.GenerateETag(streamState.Version);
        if (!string.IsNullOrEmpty(command.ETag) &&
            !ETagHelper.CheckIfMatch(context, currentETag))
        {
            return ETagHelper.PreconditionFailed();
        }

        var aggregate = await session.Events.AggregateStreamAsync<CategoryAggregate>(command.Id);
        if (aggregate == null)
        {
            return Results.NotFound();
        }

        var translations = command.Translations.ToDictionary(
            kvp => kvp.Key,
            kvp => new CategoryTranslation(kvp.Value.Name, kvp.Value.Description));

        var @event = aggregate.Update(translations);
        _ = session.Events.Append(command.Id, @event);

        var newStreamState = await session.Events.FetchStreamStateAsync(command.Id);
        var newETag = ETagHelper.GenerateETag(newStreamState!.Version);
        ETagHelper.AddETagHeader(context, newETag);

        return Results.NoContent();
    }

    public static async Task<IResult> Handle(
        SoftDeleteCategory command,
        IDocumentSession session,
        HttpContext context)
    {
        var streamState = await session.Events.FetchStreamStateAsync(command.Id);
        if (streamState == null)
        {
            return Results.NotFound();
        }

        var currentETag = ETagHelper.GenerateETag(streamState.Version);
        if (!string.IsNullOrEmpty(command.ETag) &&
            !ETagHelper.CheckIfMatch(context, currentETag))
        {
            return ETagHelper.PreconditionFailed();
        }

        var aggregate = await session.Events.AggregateStreamAsync<CategoryAggregate>(command.Id);
        if (aggregate == null)
        {
            return Results.NotFound();
        }

        var @event = aggregate.SoftDelete();
        _ = session.Events.Append(command.Id, @event);

        var newStreamState = await session.Events.FetchStreamStateAsync(command.Id);
        var newETag = ETagHelper.GenerateETag(newStreamState!.Version);
        ETagHelper.AddETagHeader(context, newETag);

        return Results.NoContent();
    }

    public static async Task<IResult> Handle(
        RestoreCategory command,
        IDocumentSession session,
        HttpContext context)
    {
        var streamState = await session.Events.FetchStreamStateAsync(command.Id);
        if (streamState == null)
        {
            return Results.NotFound();
        }

        var currentETag = ETagHelper.GenerateETag(streamState.Version);
        if (!string.IsNullOrEmpty(command.ETag) &&
            !ETagHelper.CheckIfMatch(context, currentETag))
        {
            return ETagHelper.PreconditionFailed();
        }

        var aggregate = await session.Events.AggregateStreamAsync<CategoryAggregate>(command.Id);
        if (aggregate == null)
        {
            return Results.NotFound();
        }

        var @event = aggregate.Restore();
        _ = session.Events.Append(command.Id, @event);

        var newStreamState = await session.Events.FetchStreamStateAsync(command.Id);
        var newETag = ETagHelper.GenerateETag(newStreamState!.Version);
        ETagHelper.AddETagHeader(context, newETag);

        return Results.NoContent();
    }
}
