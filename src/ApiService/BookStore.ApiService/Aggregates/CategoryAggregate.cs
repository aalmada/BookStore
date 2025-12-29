using BookStore.ApiService.Events;

namespace BookStore.ApiService.Aggregates;

public class CategoryAggregate
{
    public Guid Id { get; private set; }
    public Dictionary<string, CategoryTranslation> Translations { get; private set; } = [];
    public bool IsDeleted { get; private set; }

    // Marten uses this for rehydration
    void Apply(CategoryAdded @event)
    {
        Id = @event.Id;
        Translations = @event.Translations ?? [];
        IsDeleted = false;
    }

    void Apply(CategoryUpdated @event) => Translations = @event.Translations ?? [];

    void Apply(CategorySoftDeleted _) => IsDeleted = true;

    void Apply(CategoryRestored _) => IsDeleted = false;

    // Command methods
    public static CategoryAdded Create(Guid id, Dictionary<string, CategoryTranslation> translations)
    {
        if (translations.Count == 0)
        {
            throw new ArgumentException("At least one localized name is required", nameof(translations));
        }

        return new CategoryAdded(id, translations, DateTimeOffset.UtcNow);
    }

    public CategoryUpdated Update(Dictionary<string, CategoryTranslation> translations)
    {
        if (IsDeleted)
        {
            throw new InvalidOperationException("Cannot update a deleted category");
        }

        if (translations.Count == 0)
        {
            throw new ArgumentException("At least one localized name is required", nameof(translations));
        }

        return new CategoryUpdated(Id, translations, DateTimeOffset.UtcNow);
    }

    public CategorySoftDeleted SoftDelete()
    {
        if (IsDeleted)
        {
            throw new InvalidOperationException("Category is already deleted");
        }

        return new CategorySoftDeleted(Id, DateTimeOffset.UtcNow);
    }

    public CategoryRestored Restore()
    {
        if (!IsDeleted)
        {
            throw new InvalidOperationException("Category is not deleted");
        }

        return new CategoryRestored(Id, DateTimeOffset.UtcNow);
    }
}
