namespace BookStore.ApiService.Projections;

using Marten.Events.Projections;

// T is the view type, TId is the ID type of the view
public class {Summary}Projection : MultiStreamProjection<{Summary}Projection, Guid>
{
    // Define how to map an event to a specific View ID (Identity)
    public {Summary}Projection()
    {
        // Example: One global dashboard with a fixed ID
        Identity<{Resource}Created>(e => GlobalDashboardId);
        
        // Example: Grouping by CategoryId
        // Identity<BookPublished>(e => e.CategoryId);
    }

    public static readonly Guid GlobalDashboardId = Guid.Parse("...");

    public void Apply({Resource}Created @event, {Summary}Projection view)
    {
        view.TotalCount++;
        view.LastUpdate = @event.Timestamp;
    }
}

public class {Summary}Projection 
{
    public Guid Id { get; set; }
    public int TotalCount { get; set; }
    public DateTimeOffset LastUpdate { get; set; }
}
