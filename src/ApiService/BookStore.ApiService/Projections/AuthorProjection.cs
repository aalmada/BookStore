using BookStore.ApiService.Events;
using Marten.Events.Aggregation;

namespace BookStore.ApiService.Projections;

public class AuthorProjection
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    
    // Localized field as dictionary (key = culture, value = biography)
    public Dictionary<string, string> Biographies { get; set; } = [];
    
    public DateTimeOffset LastModified { get; set; }
    
    // SingleStreamProjection methods
    public static AuthorProjection Create(AuthorAdded @event)
    {
        return new AuthorProjection
        {
            Id = @event.Id,
            Name = @event.Name,
            LastModified = @event.Timestamp,
            Biographies = @event.Translations?
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Biography) 
                ?? []
        };
    }
    
    public void Apply(AuthorUpdated @event)
    {
        Name = @event.Name;
        LastModified = @event.Timestamp;
        Biographies = @event.Translations?
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Biography) 
            ?? [];
    }
}
