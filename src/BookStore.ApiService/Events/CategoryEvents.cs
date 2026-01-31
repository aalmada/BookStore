namespace BookStore.ApiService.Events;

// Category Events
public record CategoryAdded(
    Guid Id,
    Dictionary<string, CategoryTranslation> Translations,
    DateTimeOffset Timestamp);

public record CategoryUpdated(
    Guid Id,
    Dictionary<string, CategoryTranslation> Translations,
    DateTimeOffset Timestamp);

public record CategorySoftDeleted(
    Guid Id,
    DateTimeOffset Timestamp);

public record CategoryRestored(
    Guid Id,
    DateTimeOffset Timestamp);

// Localization model for category names
public record CategoryTranslation(string Name);
