using BookStore.ApiService.Events;

namespace BookStore.ApiService.Aggregates;

using Marten.Metadata;
using Marten.Schema;

public class CategoryAggregate : ISoftDeleted
{
    // Validation constants
    public const int MaxNameLength = 100;
    public const int MaxDescriptionLength = 500;

    public Guid Id { get; private set; }
    public Dictionary<string, CategoryTranslation> Translations { get; private set; } = [];
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
    public static CategoryAdded CreateEvent(Guid id, Dictionary<string, CategoryTranslation> translations)
    {
        ArgumentNullException.ThrowIfNull(translations);

        if (translations.Count == 0)
        {
            throw new ArgumentException("At least one localized name is required", nameof(translations));
        }

        // Validate translation values and name content
        foreach (var (key, value) in translations)
        {
            if (value is null)
            {
                throw new ArgumentException($"Translation value for language '{key}' cannot be null", nameof(translations));
            }

            if (string.IsNullOrWhiteSpace(value.Name))
            {
                throw new ArgumentException($"Translation name for language '{key}' cannot be null or empty", nameof(translations));
            }

            if (value.Name.Length > MaxNameLength)
            {
                throw new ArgumentException($"Translation name for language '{key}' cannot exceed {MaxNameLength} characters", nameof(translations));
            }
        }

        return new CategoryAdded(id, translations, DateTimeOffset.UtcNow);
    }

    public CategoryUpdated UpdateEvent(Dictionary<string, CategoryTranslation> translations)
    {
        if (Deleted)
        {
            throw new InvalidOperationException("Cannot update a deleted category");
        }

        ArgumentNullException.ThrowIfNull(translations);

        if (translations.Count == 0)
        {
            throw new ArgumentException("At least one localized name is required", nameof(translations));
        }

        // Validate translation values and name content
        foreach (var (key, value) in translations)
        {
            if (value is null)
            {
                throw new ArgumentException($"Translation value for language '{key}' cannot be null", nameof(translations));
            }

            if (string.IsNullOrWhiteSpace(value.Name))
            {
                throw new ArgumentException($"Translation name for language '{key}' cannot be null or empty", nameof(translations));
            }

            if (value.Name.Length > MaxNameLength)
            {
                throw new ArgumentException($"Translation name for language '{key}' cannot exceed {MaxNameLength} characters", nameof(translations));
            }
        }

        return new CategoryUpdated(Id, translations, DateTimeOffset.UtcNow);
    }

    public CategorySoftDeleted SoftDeleteEvent()
    {
        if (Deleted)
        {
            throw new InvalidOperationException("Category is already deleted");
        }

        return new CategorySoftDeleted(Id, DateTimeOffset.UtcNow);
    }

    public CategoryRestored RestoreEvent()
    {
        if (!Deleted)
        {
            throw new InvalidOperationException("Category is not deleted");
        }

        return new CategoryRestored(Id, DateTimeOffset.UtcNow);
    }
}
