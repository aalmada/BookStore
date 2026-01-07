using BookStore.ApiService.Events;
using Marten.Events.Aggregation;

namespace BookStore.ApiService.Projections;

public class CategoryProjection
{
    public Guid Id { get; set; }
    
    // Localized field as dictionary (key = culture, value = name)
    public Dictionary<string, string> Names { get; set; } = [];
    
    public DateTimeOffset LastModified { get; set; }
    
    // SingleStreamProjection methods
    public static CategoryProjection Create(CategoryAdded @event)
    {
        return new CategoryProjection
        {
            Id = @event.Id,
            LastModified = @event.Timestamp,
            Names = @event.Translations?
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Name) 
                ?? []
        };
    }
    
    public void Apply(CategoryUpdated @event)
    {
        LastModified = @event.Timestamp;
        Names = @event.Translations?
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Name) 
            ?? [];
    }
}
