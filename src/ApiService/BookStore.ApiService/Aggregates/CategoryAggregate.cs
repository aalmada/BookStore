using BookStore.ApiService.Events;

namespace BookStore.ApiService.Aggregates;

public class CategoryAggregate
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Dictionary<string, CategoryTranslation> Translations { get; private set; } = [];
    public bool IsDeleted { get; private set; }

    // Marten uses this for rehydration
    void Apply(CategoryAdded @event)
    {
        Id = @event.Id;
        Name = @event.Name;
        Description = @event.Description;
        Translations = @event.Translations ?? [];
        IsDeleted = false;
    }

    void Apply(CategoryUpdated @event)
    {
        Name = @event.Name;
        Description = @event.Description;
        Translations = @event.Translations ?? [];
    }

    void Apply(CategorySoftDeleted @event) => IsDeleted = true;

    void Apply(CategoryRestored @event) => IsDeleted = false;

    // Command methods
    public static CategoryAdded Create(Guid id, string name, string? description, Dictionary<string, CategoryTranslation>? translations = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required", nameof(name));
        }

        return new CategoryAdded(id, name, description, translations ?? [], DateTimeOffset.UtcNow);
    }

    public CategoryUpdated Update(string name, string? description, Dictionary<string, CategoryTranslation>? translations = null)
    {
        if (IsDeleted)
        {
            throw new InvalidOperationException("Cannot update a deleted category");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required", nameof(name));
        }

        return new CategoryUpdated(Id, name, description, translations ?? [], DateTimeOffset.UtcNow);
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
