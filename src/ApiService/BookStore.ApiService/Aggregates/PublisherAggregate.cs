using BookStore.ApiService.Events;

namespace BookStore.ApiService.Aggregates;

public class PublisherAggregate
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public bool IsDeleted { get; private set; }

    // Marten uses this for rehydration
    void Apply(PublisherAdded @event)
    {
        Id = @event.Id;
        Name = @event.Name;
        IsDeleted = false;
    }

    void Apply(PublisherUpdated @event) => Name = @event.Name;

    void Apply(PublisherSoftDeleted _) => IsDeleted = true;

    void Apply(PublisherRestored _) => IsDeleted = false;

    // Command methods
    public static PublisherAdded Create(Guid id, string name)
    {
        ValidateName(name);

        return new PublisherAdded(id, name, DateTimeOffset.UtcNow);
    }

    public PublisherUpdated Update(string name)
    {
        // Business rule: cannot update deleted publisher
        if (IsDeleted)
        {
            throw new InvalidOperationException("Cannot update a deleted publisher");
        }

        ValidateName(name);

        return new PublisherUpdated(Id, name, DateTimeOffset.UtcNow);
    }

    // Validation helper method
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

    public PublisherSoftDeleted SoftDelete()
    {
        if (IsDeleted)
        {
            throw new InvalidOperationException("Publisher is already deleted");
        }

        return new PublisherSoftDeleted(Id, DateTimeOffset.UtcNow);
    }

    public PublisherRestored Restore()
    {
        if (!IsDeleted)
        {
            throw new InvalidOperationException("Publisher is not deleted");
        }

        return new PublisherRestored(Id, DateTimeOffset.UtcNow);
    }
}
