using BookStore.ApiService.Events;
using Marten.Events.Aggregation;

namespace BookStore.ApiService.Projections;

public class CategoryProjection
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, CategoryTranslation> Translations { get; set; } = [];
    public DateTimeOffset LastModified { get; set; }
}

public class CategoryProjectionBuilder : SingleStreamProjection<CategoryProjection, Guid>
{
    public CategoryProjectionBuilder()
    {
        // Delete projection when category is soft-deleted
        DeleteEvent<CategorySoftDeleted>();
    }
    public CategoryProjection Create(CategoryAdded @event)
    {
        return new CategoryProjection
        {
            Id = @event.Id,
            Name = @event.Name,
            Translations = @event.Translations,
            LastModified = @event.Timestamp
        };
    }

    void Apply(CategoryUpdated @event, CategoryProjection projection)
    {
        projection.Name = @event.Name;
        projection.Translations = @event.Translations;
        projection.LastModified = @event.Timestamp;
    }

    // Projection will be deleted on CategorySoftDeleted (configured in constructor)
    // Projection will be recreated on CategoryRestored by replaying the stream
}
