namespace BookStore.ApiService.Events;

public record {Resource}Deleted(
    Guid Id,
    // Add reason if needed
    DateTimeOffset DeletedAt
);
