using BookStore.ApiService.Events;
using BookStore.ApiService.Infrastructure;

namespace BookStore.ApiService.Aggregates;

public class AuthorAggregate
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public Dictionary<string, AuthorTranslation> Translations { get; private set; } = [];
    public bool IsDeleted { get; private set; }

    // Marten uses this for rehydration
    void Apply(AuthorAdded @event)
    {
        Id = @event.Id;
        Name = @event.Name;
        Translations = @event.Translations;
        IsDeleted = false;
    }

    void Apply(AuthorUpdated @event)
    {
        Name = @event.Name;
        Translations = @event.Translations;
    }

    void Apply(AuthorSoftDeleted _) => IsDeleted = true;

    void Apply(AuthorRestored _) => IsDeleted = false;

    // Command methods
    public static AuthorAdded CreateEvent(Guid id, string name, Dictionary<string, AuthorTranslation> translations)
    {
        ValidateName(name);
        ValidateTranslations(translations);

        return new AuthorAdded(id, name, translations, DateTimeOffset.UtcNow);
    }

    public AuthorUpdated UpdateEvent(string name, Dictionary<string, AuthorTranslation> translations)
    {
        // Business rule: cannot update deleted author
        if (IsDeleted)
        {
            throw new InvalidOperationException("Cannot update a deleted author");
        }

        ValidateName(name);
        ValidateTranslations(translations);

        return new AuthorUpdated(Id, name, translations, DateTimeOffset.UtcNow);
    }

    // Validation constants
    public const int MaxBiographyLength = 5000;

    // Validation helper methods
    static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Author name cannot be null or empty", nameof(name));
        }

        if (name.Length > 200)
        {
            throw new ArgumentException("Name cannot exceed 200 characters", nameof(name));
        }
    }

    static void ValidateTranslations(Dictionary<string, AuthorTranslation> translations)
    {
        ArgumentNullException.ThrowIfNull(translations);

        if (translations.Count == 0)
        {
            throw new ArgumentException("At least one biography translation is required", nameof(translations));
        }

        // NOTE: We do NOT validate for a specific default language here because:
        // 1. Configuration can change over time (e.g., default language changes from "en" to "pt")
        // 2. During projection rebuilds, old events must remain valid
        // 3. The handler layer validates default language presence before creating events

        // Validate language codes
        if (!CultureValidator.ValidateTranslations(translations, out var invalidCodes))
        {
            throw new ArgumentException(
                $"Invalid language codes in biographies: {string.Join(", ", invalidCodes)}",
                nameof(translations));
        }

        // Validate translation values and biography content
        foreach (var (languageCode, translation) in translations)
        {
            if (translation is null)
            {
                throw new ArgumentException($"Translation value for language '{languageCode}' cannot be null", nameof(translations));
            }

            if (string.IsNullOrWhiteSpace(translation.Biography))
            {
                throw new ArgumentException($"Biography for language '{languageCode}' cannot be null or empty", nameof(translations));
            }

            if (translation.Biography.Length > MaxBiographyLength)
            {
                throw new ArgumentException(
                    $"Biography for language '{languageCode}' cannot exceed {MaxBiographyLength} characters",
                    nameof(translations));
            }
        }
    }

    public AuthorSoftDeleted SoftDeleteEvent()
    {
        if (IsDeleted)
        {
            throw new InvalidOperationException("Author is already deleted");
        }

        return new AuthorSoftDeleted(Id, DateTimeOffset.UtcNow);
    }

    public AuthorRestored RestoreEvent()
    {
        if (!IsDeleted)
        {
            throw new InvalidOperationException("Author is not deleted");
        }

        return new AuthorRestored(Id, DateTimeOffset.UtcNow);
    }
}
