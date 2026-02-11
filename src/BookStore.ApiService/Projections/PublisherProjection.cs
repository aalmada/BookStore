using BookStore.ApiService.Events;
using JasperFx.Events;
using Marten.Events;
using Marten.Events.Aggregation;

namespace BookStore.ApiService.Projections;

using Marten.Metadata;

public class PublisherProjection
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset LastModified { get; set; }
    public bool Deleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public long Version { get; set; }

    // SingleStreamProjection methods
    public static PublisherProjection Create(IEvent<PublisherAdded> @event) => new()
    {
        Id = @event.Data.Id,
        Name = @event.Data.Name,
        LastModified = @event.Timestamp,
        Version = @event.Version
    };

    public void Apply(IEvent<PublisherUpdated> @event)
    {
        Name = @event.Data.Name;
        LastModified = @event.Timestamp;
        Version = @event.Version;
    }

    public void Apply(IEvent<PublisherSoftDeleted> @event)
    {
        LastModified = @event.Timestamp;
        Version = @event.Version;
        Deleted = true;
        DeletedAt = @event.Timestamp;
    }

    public void Apply(IEvent<PublisherRestored> @event)
    {
        LastModified = @event.Timestamp;
        Version = @event.Version;
        Deleted = false;
        DeletedAt = null;
    }
}
