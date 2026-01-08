using BookStore.ApiService.Events;
using Marten.Events.Aggregation;

namespace BookStore.ApiService.Projections;

public class CategoryProjection
{
    public Guid Id { get; set; }

    // Localized field as dictionary (key = culture, value = name)
    public Dictionary<string, string> Names { get; set; } = [];

    public DateTimeOffset LastModified { get; set; }
    public bool IsDeleted { get; set; }

    // SingleStreamProjection methods
    public static CategoryProjection Create(CategoryAdded @event) => new()
    {
        Id = @event.Id,
        LastModified = @event.Timestamp,
        IsDeleted = false,
        Names = @event.Translations?
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Name)
                ?? []
    };

    public void Apply(CategoryUpdated @event)
    {
        LastModified = @event.Timestamp;
        Names = @event.Translations?
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Name)
            ?? [];
    }

    public void Apply(CategorySoftDeleted @event)
    {
        LastModified = @event.Timestamp;
        IsDeleted = true;
    }

    public void Apply(CategoryRestored @event)
    {
        LastModified = @event.Timestamp;
        IsDeleted = false;
    }
}
