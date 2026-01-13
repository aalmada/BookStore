using BookStore.ApiService.Events;
using Marten.Events.Aggregation;

namespace BookStore.ApiService.Projections;

using Marten.Metadata;

public class AuthorProjection
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    // Localized field as dictionary (key = culture, value = biography)
    public Dictionary<string, string> Biographies { get; set; } = [];

    public DateTimeOffset LastModified { get; set; }
    public bool Deleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    // SingleStreamProjection methods
    public static AuthorProjection Create(AuthorAdded @event) => new()
    {
        Id = @event.Id,
        Name = @event.Name,
        LastModified = @event.Timestamp,
        Biographies = @event.Translations?
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Biography)
                ?? []
    };

    public void Apply(AuthorUpdated @event)
    {
        Name = @event.Name;
        LastModified = @event.Timestamp;
        Biographies = @event.Translations?
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Biography)
            ?? [];
    }

    public void Apply(AuthorSoftDeleted @event)
    {
        LastModified = @event.Timestamp;
        Deleted = true;
        DeletedAt = @event.Timestamp;
    }

    public void Apply(AuthorRestored @event)
    {
        LastModified = @event.Timestamp;
        Deleted = false;
        DeletedAt = null;
    }
}
