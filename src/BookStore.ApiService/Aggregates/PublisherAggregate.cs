using BookStore.ApiService.Events;

namespace BookStore.ApiService.Aggregates;

using Marten.Metadata;
using Marten.Schema;

public class PublisherAggregate : ISoftDeleted
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
#pragma warning disable BS3005 // Aggregate properties must have private setters
    public bool Deleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
#pragma warning restore BS3005

    // Marten uses this for rehydration
    void Apply(PublisherAdded @event)
    {
        Id = @event.Id;
        Name = @event.Name;
        Deleted = false;
    }

    void Apply(PublisherUpdated @event) => Name = @event.Name;

    void Apply(PublisherSoftDeleted @event)
    {
        Deleted = true;
        DeletedAt = @event.Timestamp;
    }

    void Apply(PublisherRestored _)
    {
        Deleted = false;
        DeletedAt = null;
    }

    // Command methods
    public static PublisherAdded CreateEvent(Guid id, string name)
    {
        ValidateName(name);

        return new PublisherAdded(id, name, DateTimeOffset.UtcNow);
    }

    public PublisherUpdated UpdateEvent(string name)
    {
        // Business rule: cannot update deleted publisher
        if (Deleted)
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

    public PublisherSoftDeleted SoftDeleteEvent()
    {
        if (Deleted)
        {
            throw new InvalidOperationException("Publisher is already deleted");
        }

        return new PublisherSoftDeleted(Id, DateTimeOffset.UtcNow);
    }

    public PublisherRestored RestoreEvent()
    {
        if (!Deleted)
        {
            throw new InvalidOperationException("Publisher is not deleted");
        }

        return new PublisherRestored(Id, DateTimeOffset.UtcNow);
    }
}
