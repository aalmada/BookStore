using BookStore.ApiService.Events;
using JasperFx.Events;
using Marten.Events.Projections;
using Marten.Storage;

namespace BookStore.ApiService.Projections;

// EventProjection: 1 event → 1 (or more) documents.
// Best for audit logs, history tables, and reporting side-tables.
// Register with: options.Projections.Add<{Resource}AuditProjection>(ProjectionLifecycle.Async);
public class {Resource}AuditProjection : EventProjection
{
    // Convention method: Create() returns the document to store.
    // Use IEvent<T> to access event metadata (Timestamp, StreamId, Version).
    public {Resource}LogEntry Create(IEvent<{Resource}Added> @event) => new()
    {
        Id = Guid.CreateVersion7(),
        ResourceId = @event.Data.Id,
        Timestamp = @event.Timestamp,
        Action = "Added"
    };

    // Convention method: Project() for operations that don't create a single new doc.
    public void Project(IEvent<{Resource}Updated> @event, IDocumentOperations ops)
    {
        var logEntry = new {Resource}LogEntry
        {
            Id = Guid.CreateVersion7(),
            ResourceId = @event.Data.Id,
            Timestamp = @event.Timestamp,
            Action = "Updated"
        };
        ops.Store(logEntry);
    }

    public void Project(IEvent<{Resource}SoftDeleted> @event, IDocumentOperations ops)
    {
        var logEntry = new {Resource}LogEntry
        {
            Id = Guid.CreateVersion7(),
            ResourceId = @event.Data.Id,
            Timestamp = @event.Timestamp,
            Action = "SoftDeleted"
        };
        ops.Store(logEntry);
    }
}

// The document type stored by this projection.
public class {Resource}LogEntry
{
    public Guid Id { get; set; }
    public Guid ResourceId { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string Action { get; set; } = string.Empty;
}
