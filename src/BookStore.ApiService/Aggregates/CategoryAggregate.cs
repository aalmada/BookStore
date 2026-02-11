using BookStore.ApiService.Events;

namespace BookStore.ApiService.Aggregates;

using Marten.Metadata;
using Marten.Schema;

public class CategoryAggregate : ISoftDeleted
{
    // Validation constants
    public const int MaxNameLength = 100;

    public Guid Id { get; private set; }
    public Dictionary<string, CategoryTranslation> Translations { get; private set; } = [];
    public long Version { get; private set; }

#pragma warning disable BS3005 // Aggregate properties must have private setters
    public bool Deleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
#pragma warning restore BS3005

    // Marten uses this for rehydration
    void Apply(CategoryAdded @event)
    {
        Id = @event.Id;
        Translations = @event.Translations ?? [];
        Deleted = false;
    }

    void Apply(CategoryUpdated @event) => Translations = @event.Translations ?? [];

    void Apply(CategorySoftDeleted @event)
    {
        Deleted = true;
        DeletedAt = @event.Timestamp;
    }

    void Apply(CategoryRestored _)
    {
        Deleted = false;
        DeletedAt = null;
    }

    // Command methods
    public static Result<CategoryAdded> CreateEvent(Guid id, Dictionary<string, CategoryTranslation> translations)
    {
        if (id == Guid.Empty)
        {
            return Result.Failure<CategoryAdded>(Error.Validation(ErrorCodes.Categories.IdRequired, "Category ID is required and cannot be empty"));
        }

        var translationsResult = ValidateTranslations(translations);
        if (translationsResult.IsFailure)
        {
            return Result.Failure<CategoryAdded>(translationsResult.Error);
        }

        return new CategoryAdded(id, translations, DateTimeOffset.UtcNow);
    }

    public Result<CategoryUpdated> UpdateEvent(Dictionary<string, CategoryTranslation> translations)
    {
        if (Deleted)
        {
            return Result.Failure<CategoryUpdated>(Error.Conflict(ErrorCodes.Categories.AlreadyDeleted, "Cannot update a deleted category"));
        }

        var translationsResult = ValidateTranslations(translations);
        if (translationsResult.IsFailure)
        {
            return Result.Failure<CategoryUpdated>(translationsResult.Error);
        }

        return new CategoryUpdated(Id, translations, DateTimeOffset.UtcNow);
    }

    static Result ValidateTranslations(Dictionary<string, CategoryTranslation> translations)
    {
        if (translations is null)
        {
            return Result.Failure(Error.Validation(ErrorCodes.Categories.TranslationsRequired, "Translations cannot be null"));
        }

        if (translations.Count == 0)
        {
            return Result.Failure(Error.Validation(ErrorCodes.Categories.TranslationsRequired, "At least one localized name is required"));
        }

        // Validate translation values and name content
        foreach (var (key, value) in translations)
        {
            if (value is null)
            {
                return Result.Failure(Error.Validation(ErrorCodes.Categories.TranslationValueRequired, $"Translation value for language '{key}' cannot be null"));
            }

            if (string.IsNullOrWhiteSpace(value.Name))
            {
                return Result.Failure(Error.Validation(ErrorCodes.Categories.NameRequired, $"Translation name for language '{key}' cannot be null or empty"));
            }

            if (value.Name.Length > MaxNameLength)
            {
                return Result.Failure(Error.Validation(ErrorCodes.Categories.NameTooLong, $"Translation name for language '{key}' cannot exceed {MaxNameLength} characters"));
            }
        }

        return Result.Success();
    }

    public Result<CategorySoftDeleted> SoftDeleteEvent()
    {
        if (Deleted)
        {
            return Result.Failure<CategorySoftDeleted>(Error.Conflict(ErrorCodes.Categories.AlreadyDeleted, "Category is already deleted"));
        }

        return new CategorySoftDeleted(Id, DateTimeOffset.UtcNow);
    }

    public Result<CategoryRestored> RestoreEvent()
    {
        if (!Deleted)
        {
            return Result.Failure<CategoryRestored>(Error.Conflict(ErrorCodes.Categories.NotDeleted, "Category is not deleted"));
        }

        return new CategoryRestored(Id, DateTimeOffset.UtcNow);
    }
}
