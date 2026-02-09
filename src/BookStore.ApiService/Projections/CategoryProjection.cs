using JasperFx.Events;
using BookStore.ApiService.Events;
using BookStore.Shared.Infrastructure;
using Marten.Events;
using Marten.Events.Aggregation;

namespace BookStore.ApiService.Projections;

using Marten.Metadata;

public class CategoryProjection
{
    public Guid Id { get; set; }

    // Localized field as dictionary (key = culture, value = name)
    public Dictionary<string, string> Names { get; set; } = [];

    public DateTimeOffset LastModified { get; set; }
    public bool Deleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public long Version { get; set; }

    // SingleStreamProjection methods
    public static CategoryProjection Create(IEvent<CategoryAdded> @event) => new()
    {
        Id = @event.Data.Id,
        LastModified = @event.Timestamp,
        Version = @event.Version,
        Deleted = false,
        Names = @event.Data.Translations?
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Name)
                ?? []
    };

    public void Apply(IEvent<CategoryUpdated> @event)
    {
        LastModified = @event.Timestamp;
        Version = @event.Version;
        Names = @event.Data.Translations?
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Name)
            ?? [];
    }

    public void Apply(IEvent<CategorySoftDeleted> @event)
    {
        LastModified = @event.Timestamp;
        Version = @event.Version;
        Deleted = true;
        DeletedAt = @event.Timestamp;
    }

    public void Apply(IEvent<CategoryRestored> @event)
    {
        LastModified = @event.Timestamp;
        Version = @event.Version;
        Deleted = false;
        DeletedAt = null;
    }
}
