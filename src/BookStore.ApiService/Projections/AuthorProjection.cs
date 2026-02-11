using BookStore.ApiService.Events;
using BookStore.Shared.Infrastructure;
using JasperFx.Events;
using Marten.Events;
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
    public long Version { get; set; }

    // SingleStreamProjection methods
    public static AuthorProjection Create(IEvent<AuthorAdded> @event) => new()
    {
        Id = @event.Data.Id,
        Name = @event.Data.Name,
        LastModified = @event.Timestamp,
        Version = @event.Version,
        Biographies = @event.Data.Translations?
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Biography)
                ?? []
    };

    public void Apply(IEvent<AuthorUpdated> @event)
    {
        Name = @event.Data.Name;
        LastModified = @event.Timestamp;
        Version = @event.Version;
        Biographies = @event.Data.Translations?
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Biography)
            ?? [];
    }

    public void Apply(IEvent<AuthorSoftDeleted> @event)
    {
        LastModified = @event.Timestamp;
        Version = @event.Version;
        Deleted = true;
        DeletedAt = @event.Timestamp;
    }

    public void Apply(IEvent<AuthorRestored> @event)
    {
        LastModified = @event.Timestamp;
        Version = @event.Version;
        Deleted = false;
        DeletedAt = null;
    }
}
