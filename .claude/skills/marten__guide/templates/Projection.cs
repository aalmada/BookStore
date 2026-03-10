// ── Option A: POCO Snapshot (most common) ──────────────────────────────────
// No base class required. Register with:
//   options.Projections.Snapshot<{Resource}Projection>(SnapshotLifecycle.Async);
//
// Use IEvent<T> wrappers in Apply methods to access event metadata (Timestamp, Version).

using BookStore.ApiService.Events;
using JasperFx.Events;
using Marten.Metadata;

namespace BookStore.ApiService.Projections;

public class {Resource}Projection
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset LastModified { get; set; }
    public bool Deleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public long Version { get; set; }

    // Marten calls Create() to build the initial snapshot
    public static {Resource}Projection Create(IEvent<{Resource}Added> @event) => new()
    {
        Id = @event.Data.Id,
        Name = @event.Data.Name,
        LastModified = @event.Timestamp,
        Version = @event.Version
    };

    // Marten calls Apply() for subsequent events
    public void Apply(IEvent<{Resource}Updated> @event)
    {
        Name = @event.Data.Name;
        LastModified = @event.Timestamp;
        Version = @event.Version;
    }

    public void Apply(IEvent<{Resource}SoftDeleted> @event)
    {
        Deleted = true;
        DeletedAt = @event.Timestamp;
        LastModified = @event.Timestamp;
        Version = @event.Version;
    }

    public void Apply(IEvent<{Resource}Restored> @event)
    {
        Deleted = false;
        DeletedAt = null;
        LastModified = @event.Timestamp;
        Version = @event.Version;
    }
}

// ── Option B: SingleStreamProjection (explicit control) ─────────────────────
// Use when you need IncludeType<T>(), DeleteEvent<T>(), or DetermineAction().
// Register with:
//   options.Projections.Add<{Resource}Projection>(ProjectionLifecycle.Async);
//
// public class {Resource}Projection : SingleStreamProjection<{Resource}Projection, Guid>
// {
//     public {Resource}Projection()
//     {
//         Options.CacheLimitPerTenant = 1000;
//         IncludeType<{Resource}Added>();
//         IncludeType<{Resource}Updated>();
//         IncludeType<{Resource}SoftDeleted>();
//         IncludeType<{Resource}Restored>();
//     }
//     // ... same Create / Apply methods as above
// }
