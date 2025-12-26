using BookStore.ApiService.Events;
using Marten.Events.Aggregation;

namespace BookStore.ApiService.Projections;

public class PublisherProjection
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset LastModified { get; set; }
}

public class PublisherProjectionBuilder : SingleStreamProjection<PublisherProjection, Guid>
{
    public PublisherProjectionBuilder()
    {
        // Delete projection when publisher is soft-deleted
        DeleteEvent<PublisherSoftDeleted>();
    }
    public PublisherProjection Create(PublisherAdded @event)
    {
        return new PublisherProjection
        {
            Id = @event.Id,
            Name = @event.Name,
            LastModified = @event.Timestamp
        };
    }

    void Apply(PublisherUpdated @event, PublisherProjection projection)
    {
        projection.Name = @event.Name;
        projection.LastModified = @event.Timestamp;
    }

    // Projection will be deleted on PublisherSoftDeleted (configured in constructor)
    // Projection will be recreated on PublisherRestored by replaying the stream
}
