namespace BookStore.ApiService.Events;

public record {Resource}Created(
    Guid Id,
    string Name,
    // Add other initial properties
    DateTimeOffset CreatedAt
);
