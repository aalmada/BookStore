namespace BookStore.ApiService.Projections;

using Marten.Events.Aggregation;

public class {Resource}Projection : SingleStreamProjection<{Resource}Projection>
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Version { get; set; }

    // Define creation from the initial event
    public static {Resource}Projection Create({Resource}Created @event)
    {
        return new {Resource}Projection
        {
            Id = @event.Id,
            Name = @event.Name,
            Version = 1
        };
    }

    // Define updates from subsequent events
    public void Apply({Resource}Updated @event)
    {
        Name = @event.Name;
        Version++;
    }
}
