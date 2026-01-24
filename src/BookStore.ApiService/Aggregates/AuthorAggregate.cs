using BookStore.ApiService.Events;
using BookStore.ApiService.Infrastructure;

namespace BookStore.ApiService.Aggregates;

using Marten.Metadata;
using Marten.Schema;

public class AuthorAggregate : ISoftDeleted
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public Dictionary<string, AuthorTranslation> Translations { get; private set; } = [];
#pragma warning disable BS3005 // Aggregate properties must have private setters (Marten ISoftDeleted requirement)
    public bool Deleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
#pragma warning restore BS3005

    // Marten uses this for rehydration
    void Apply(AuthorAdded @event)
    {
        Id = @event.Id;
        Name = @event.Name;
        Translations = @event.Translations;
        Deleted = false;
    }

    void Apply(AuthorUpdated @event)
    {
        Name = @event.Name;
        Translations = @event.Translations;
    }

    void Apply(AuthorSoftDeleted @event)
    {
        Deleted = true;
        DeletedAt = @event.Timestamp;
    }

    void Apply(AuthorRestored _)
    {
        Deleted = false;
        DeletedAt = null;
    }

    // Command methods
    public static Result<AuthorAdded> CreateEvent(Guid id, string name, Dictionary<string, AuthorTranslation> translations)
    {
        var nameResult = ValidateName(name);
        if (nameResult.IsFailure)
        {
            return Result.Failure<AuthorAdded>(nameResult.Error);
        }

        var translationsResult = ValidateTranslations(translations);
        if (translationsResult.IsFailure)
        {
            return Result.Failure<AuthorAdded>(translationsResult.Error);
        }

        return new AuthorAdded(id, name, translations, DateTimeOffset.UtcNow);
    }

    public Result<AuthorUpdated> UpdateEvent(string name, Dictionary<string, AuthorTranslation> translations)
    {
        // Business rule: cannot update deleted author
        if (Deleted)
        {
            return Result.Failure<AuthorUpdated>(Error.Conflict(ErrorCodes.Authors.AlreadyDeleted, "Cannot update a deleted author"));
        }

        var nameResult = ValidateName(name);
        if (nameResult.IsFailure)
        {
            return Result.Failure<AuthorUpdated>(nameResult.Error);
        }

        var translationsResult = ValidateTranslations(translations);
        if (translationsResult.IsFailure)
        {
            return Result.Failure<AuthorUpdated>(translationsResult.Error);
        }

        return new AuthorUpdated(Id, name, translations, DateTimeOffset.UtcNow);
    }

    // Validation constants
    public const int MaxBiographyLength = 5000;

    // Validation helper methods
    static Result ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure(Error.Validation(ErrorCodes.Authors.NameRequired, "Author name cannot be null or empty"));
        }

        if (name.Length > 200)
        {
            return Result.Failure(Error.Validation(ErrorCodes.Authors.NameTooLong, "Name cannot exceed 200 characters"));
        }

        return Result.Success();
    }

    static Result ValidateTranslations(Dictionary<string, AuthorTranslation> translations)
    {
        if (translations is null)
        {
            return Result.Failure(Error.Validation(ErrorCodes.Authors.TranslationsRequired, "Translations cannot be null"));
        }

        if (translations.Count == 0)
        {
            return Result.Failure(Error.Validation(ErrorCodes.Authors.TranslationsRequired, "At least one biography translation is required"));
        }

        // Validate language codes
        if (!CultureValidator.ValidateTranslations(translations, out var invalidCodes))
        {
            return Result.Failure(Error.Validation(ErrorCodes.Authors.TranslationLanguageInvalid, $"Invalid language codes in biographies: {string.Join(", ", invalidCodes)}"));
        }

        // Validate translation values and biography content
        foreach (var (languageCode, translation) in translations)
        {
            if (translation is null)
            {
                return Result.Failure(Error.Validation(ErrorCodes.Authors.TranslationValueRequired, $"Translation value for language '{languageCode}' cannot be null"));
            }

            if (string.IsNullOrWhiteSpace(translation.Biography))
            {
                return Result.Failure(Error.Validation(ErrorCodes.Authors.BiographyRequired, $"Biography for language '{languageCode}' cannot be null or empty"));
            }

            if (translation.Biography.Length > MaxBiographyLength)
            {
                return Result.Failure(Error.Validation(ErrorCodes.Authors.BiographyTooLong, $"Biography for language '{languageCode}' cannot exceed {MaxBiographyLength} characters"));
            }
        }

        return Result.Success();
    }

    public Result<AuthorSoftDeleted> SoftDeleteEvent()
    {
        if (Deleted)
        {
            return Result.Failure<AuthorSoftDeleted>(Error.Conflict(ErrorCodes.Authors.AlreadyDeleted, "Author is already deleted"));
        }

        return new AuthorSoftDeleted(Id, DateTimeOffset.UtcNow);
    }

    public Result<AuthorRestored> RestoreEvent()
    {
        if (!Deleted)
        {
            return Result.Failure<AuthorRestored>(Error.Conflict(ErrorCodes.Authors.NotDeleted, "Author is not deleted"));
        }

        return new AuthorRestored(Id, DateTimeOffset.UtcNow);
    }
}
