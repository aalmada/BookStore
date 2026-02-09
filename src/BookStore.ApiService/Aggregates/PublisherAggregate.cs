using BookStore.ApiService.Events;

namespace BookStore.ApiService.Aggregates;

using Marten.Metadata;
using Marten.Schema;

public class PublisherAggregate : ISoftDeleted
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public long Version { get; private set; }
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
    public static Result<PublisherAdded> CreateEvent(Guid id, string name)
    {
        var nameResult = ValidateName(name);
        if (nameResult.IsFailure)
        {
            return Result.Failure<PublisherAdded>(nameResult.Error);
        }

        return new PublisherAdded(id, name, DateTimeOffset.UtcNow);
    }

    public Result<PublisherUpdated> UpdateEvent(string name)
    {
        // Business rule: cannot update deleted publisher
        if (Deleted)
        {
            return Result.Failure<PublisherUpdated>(Error.Conflict(ErrorCodes.Publishers.AlreadyDeleted, "Cannot update a deleted publisher"));
        }

        var nameResult = ValidateName(name);
        if (nameResult.IsFailure)
        {
            return Result.Failure<PublisherUpdated>(nameResult.Error);
        }

        return new PublisherUpdated(Id, name, DateTimeOffset.UtcNow);
    }

    // Validation helper method
    static Result ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure(Error.Validation(ErrorCodes.Publishers.NameRequired, "Name is required"));
        }

        if (name.Length > 200)
        {
            return Result.Failure(Error.Validation(ErrorCodes.Publishers.NameTooLong, "Name cannot exceed 200 characters"));
        }

        return Result.Success();
    }

    public Result<PublisherSoftDeleted> SoftDeleteEvent()
    {
        if (Deleted)
        {
            return Result.Failure<PublisherSoftDeleted>(Error.Conflict(ErrorCodes.Publishers.AlreadyDeleted, "Publisher is already deleted"));
        }

        return new PublisherSoftDeleted(Id, DateTimeOffset.UtcNow);
    }

    public Result<PublisherRestored> RestoreEvent()
    {
        if (!Deleted)
        {
            return Result.Failure<PublisherRestored>(Error.Conflict(ErrorCodes.Publishers.NotDeleted, "Publisher is not deleted"));
        }

        return new PublisherRestored(Id, DateTimeOffset.UtcNow);
    }
}
