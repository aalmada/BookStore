using BookStore.ApiService.Events;
using Marten.Events.Aggregation;

namespace BookStore.ApiService.Projections;

public class PublisherProjection
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset LastModified { get; set; }
    
    // SingleStreamProjection methods
    public static PublisherProjection Create(PublisherAdded @event)
    {
        return new PublisherProjection
        {
            Id = @event.Id,
            Name = @event.Name,
            LastModified = @event.Timestamp
        };
    }
    
    public void Apply(PublisherUpdated @event)
    {
        Name = @event.Name;
        LastModified = @event.Timestamp;
    }
    
    // Note: Projection will be deleted on PublisherSoftDeleted
    // and recreated on PublisherRestored by replaying the stream
}
