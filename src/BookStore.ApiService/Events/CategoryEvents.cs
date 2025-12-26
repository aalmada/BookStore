namespace BookStore.ApiService.Events;

// Category Events
public record CategoryAdded(
    Guid Id,
    string Name,
    string? Description,
    Dictionary<string, CategoryTranslation> Translations,
    DateTimeOffset Timestamp);

public record CategoryUpdated(
    Guid Id,
    string Name,
    string? Description,
    Dictionary<string, CategoryTranslation> Translations,
    DateTimeOffset Timestamp);

public record CategorySoftDeleted(
    Guid Id,
    DateTimeOffset Timestamp);

public record CategoryRestored(
    Guid Id,
    DateTimeOffset Timestamp);

// Translation model for category names and descriptions
public record CategoryTranslation(string Name, string? Description);
