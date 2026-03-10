using BookStore.ApiService.Events;
using Marten.Events.Aggregation;

namespace BookStore.ApiService.Projections;

// The view document (stored as a Marten document)
public class {Summary}
{
    public Guid Id { get; set; }
    public int TotalCount { get; set; }
    public DateTimeOffset LastUpdate { get; set; }
}

// The projection builder — class name convention: {Summary}ProjectionBuilder
// Register with:
//   options.Projections.Add<{Summary}ProjectionBuilder>(ProjectionLifecycle.Async);
public class {Summary}ProjectionBuilder : MultiStreamProjection<{Summary}, Guid>
{
    public {Summary}ProjectionBuilder()
    {
        // Performance: cache up to 1000 aggregates per tenant in memory
        Options.CacheLimitPerTenant = 1000;

        // Route events to the correct {Summary} document by ID
        // Example A: each event carries the aggregate ID directly
        Identity<{Resource}Added>(e => e.Id);

        // Example B: use a common interface shared by all events
        // Identity<I{Resource}Event>(e => e.{Resource}Id);

        // Example C: one event fans out to multiple documents
        // Identities<IEvent<{Resource}Updated>>(x => x.Data.RelatedIds);

        // Example D: complex routing that requires querying the store
        // CustomGrouping(new {Summary}Grouper());
    }

    // Create the document when the first relevant event arrives
    public {Summary} Create({Resource}Added @event) => new()
    {
        Id = @event.Id,
        TotalCount = 0,
        LastUpdate = @event.Timestamp
    };

    public void Apply({Summary} view, {Resource}Added @event)
    {
        view.TotalCount++;
        view.LastUpdate = @event.Timestamp;
    }
}
