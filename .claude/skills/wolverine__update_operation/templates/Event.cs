namespace BookStore.ApiService.Events;

public record {Resource}Updated(
    Guid Id,
    string NewName,
    // Add other updated properties
    DateTimeOffset UpdatedAt
);
