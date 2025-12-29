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
        Translations = @event.Translations ?? [];
        IsDeleted = false;
    }

    void Apply(AuthorUpdated @event)
    {
        Name = @event.Name;
        Translations = @event.Translations ?? [];
    }

    void Apply(AuthorSoftDeleted _) => IsDeleted = true;

    void Apply(AuthorRestored _) => IsDeleted = false;

    // Command methods
    public static AuthorAdded Create(Guid id, string name, Dictionary<string, AuthorTranslation>? translations)
    {
        ValidateName(name);
        ValidateTranslations(translations);

        return new AuthorAdded(id, name, translations, DateTimeOffset.UtcNow);
    }

    public AuthorUpdated Update(string name, Dictionary<string, AuthorTranslation>? translations)
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

    // Validation helper methods
    static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required", nameof(name));
        }

        if (name.Length > 200)
        {
            throw new ArgumentException("Name cannot exceed 200 characters", nameof(name));
        }
    }

    static void ValidateTranslations(Dictionary<string, AuthorTranslation>? translations)
    {
        if (translations == null || translations.Count == 0)
        {
            return; // Translations are optional
        }

        // Validate language codes
        if (!CultureValidator.ValidateTranslations(translations, out var invalidCodes))
        {
            throw new ArgumentException(
                $"Invalid language codes in biographies: {string.Join(", ", invalidCodes)}",
                nameof(translations));
        }

        // Validate biography length for each translation
        foreach (var (languageCode, translation) in translations)
        {
            if (translation.Biography.Length > 5000)
            {
                throw new ArgumentException(
                    $"Biography for language '{languageCode}' cannot exceed 5000 characters",
                    nameof(translations));
            }
        }
    }

    public AuthorSoftDeleted SoftDelete()
    {
        if (IsDeleted)
        {
            throw new InvalidOperationException("Author is already deleted");
        }

        return new AuthorSoftDeleted(Id, DateTimeOffset.UtcNow);
    }

    public AuthorRestored Restore()
    {
        if (!IsDeleted)
        {
            throw new InvalidOperationException("Author is not deleted");
        }

        return new AuthorRestored(Id, DateTimeOffset.UtcNow);
    }
}
