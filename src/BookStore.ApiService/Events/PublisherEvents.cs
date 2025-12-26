namespace BookStore.ApiService.Events;

// Publisher Events
public record PublisherAdded(
    Guid Id,
    string Name,
    DateTimeOffset Timestamp);

public record PublisherUpdated(
    Guid Id,
    string Name,
    DateTimeOffset Timestamp);

public record PublisherSoftDeleted(
    Guid Id,
    DateTimeOffset Timestamp);

public record PublisherRestored(
    Guid Id,
    DateTimeOffset Timestamp);
