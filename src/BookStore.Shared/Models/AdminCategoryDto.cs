namespace BookStore.Shared.Models;

/// <summary>
/// Administrative DTO for category information, including all translations.
/// </summary>
public record AdminCategoryDto(
    Guid Id,
    string Name,
    IReadOnlyDictionary<string, CategoryTranslationDto> Translations,
    string? ETag = null);
