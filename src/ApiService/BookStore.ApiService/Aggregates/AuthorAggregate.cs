using BookStore.ApiService.Events;

namespace BookStore.ApiService.Aggregates;

public class AuthorAggregate
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Biography { get; private set; }
    public bool IsDeleted { get; private set; }

    // Marten uses this for rehydration
    void Apply(AuthorAdded @event)
    {
        Id = @event.Id;
        Name = @event.Name;
        Biography = @event.Biography;
        IsDeleted = false;
    }

    void Apply(AuthorUpdated @event)
    {
        Name = @event.Name;
        Biography = @event.Biography;
    }

    void Apply(AuthorSoftDeleted @event) => IsDeleted = true;

    void Apply(AuthorRestored @event) => IsDeleted = false;

    // Command methods
    public static AuthorAdded Create(Guid id, string name, string? biography)
    {
        ValidateName(name);
        ValidateBiography(biography);

        return new AuthorAdded(id, name, biography, DateTimeOffset.UtcNow);
    }

    public AuthorUpdated Update(string name, string? biography)
    {
        // Business rule: cannot update deleted author
        if (IsDeleted)
        {
            throw new InvalidOperationException("Cannot update a deleted author");
        }

        ValidateName(name);
        ValidateBiography(biography);

        return new AuthorUpdated(Id, name, biography, DateTimeOffset.UtcNow);
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

    static void ValidateBiography(string? biography)
    {
        if (biography != null && biography.Length > 5000)
        {
            throw new ArgumentException("Biography cannot exceed 5000 characters", nameof(biography));
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
