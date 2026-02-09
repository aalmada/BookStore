namespace BookStore.Shared.Models;

/// <summary>
/// Administrative DTO for author information, including all translations.
/// </summary>
public record AdminAuthorDto(
    Guid Id,
    string Name,
    string? Biography,
    IReadOnlyDictionary<string, AuthorTranslationDto> Translations,
    string? ETag = null);
