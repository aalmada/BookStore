namespace BookStore.ApiService.Events;

// Author Events
public record AuthorAdded(
    Guid Id,
    string Name,
    Dictionary<string, AuthorTranslation>? Translations,
    DateTimeOffset Timestamp);

public record AuthorUpdated(
    Guid Id,
    string Name,
    Dictionary<string, AuthorTranslation>? Translations,
    DateTimeOffset Timestamp);

public record AuthorSoftDeleted(
    Guid Id,
    DateTimeOffset Timestamp);

public record AuthorRestored(
    Guid Id,
    DateTimeOffset Timestamp);

// Localization model for author biographies
public record AuthorTranslation(string Biography);
