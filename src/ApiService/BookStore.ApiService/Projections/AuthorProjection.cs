using BookStore.ApiService.Events;
using Marten.Events.Aggregation;

namespace BookStore.ApiService.Projections;

public class AuthorProjection
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Biography { get; set; }
    public DateTimeOffset LastModified { get; set; }
}

public class AuthorProjectionBuilder : SingleStreamProjection<AuthorProjection, Guid>
{
    public AuthorProjectionBuilder()
    {
        // Delete projection when author is soft-deleted
        DeleteEvent<AuthorSoftDeleted>();
    }
    public AuthorProjection Create(AuthorAdded @event)
    {
        return new AuthorProjection
        {
            Id = @event.Id,
            Name = @event.Name,
            Biography = @event.Biography,
            LastModified = @event.Timestamp
        };
    }

    void Apply(AuthorUpdated @event, AuthorProjection projection)
    {
        projection.Name = @event.Name;
        projection.Biography = @event.Biography;
        projection.LastModified = @event.Timestamp;
    }

    // Projection will be deleted on AuthorSoftDeleted (configured in constructor)
    // Projection will be recreated on AuthorRestored by replaying the stream
}
