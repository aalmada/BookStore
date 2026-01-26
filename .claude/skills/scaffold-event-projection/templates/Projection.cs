namespace BookStore.ApiService.Projections;

using Marten.Events.Projections;

public class {Resource}Projection : EventProjection
{
    public {Resource}Projection()
    {
        // Define logic for specific events
        Project<{Resource}Created>((@event, ops) => 
        {
            var logEntry = new {Resource}LogEntry
            {
                Id = Guid.NewGuid(),
                ResourceId = @event.Id,
                Timestamp = @event.Timestamp,
                Action = "Created"
            };
            
            // Queue a document operation
            ops.Store(logEntry);
        });
        
        // OR use method convention
    }
    
    public void Project(IEvent<{Resource}Updated> @event, IDocumentOperations ops)
    {
         var logEntry = new {Resource}LogEntry
        {
            Id = Guid.NewGuid(),
            ResourceId = @event.Data.Id,
            Timestamp = @event.Timestamp,
            Action = "Updated"
        };
        ops.Store(logEntry);
    }
}

// The document type to be stored
public class {Resource}LogEntry
{
    public Guid Id { get; set; }
    public Guid ResourceId { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string Action { get; set; } = string.Empty;
}
