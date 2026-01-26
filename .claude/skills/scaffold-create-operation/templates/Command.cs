namespace BookStore.ApiService.Commands.{Resource};

public record Create{Resource}(string Name /* other args */)
{
    // Factory method to generate ID and timestamp appropriately
    public static ({Resource}Created Event, Guid Id) CreateEvent(string name)
    {
        var id = Guid.CreateVersion7();
        var @event = new {Resource}Created(
            id,
            name,
            DateTimeOffset.UtcNow
        );
        return (@event, id);
    }
}
