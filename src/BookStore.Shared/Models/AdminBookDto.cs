using System.Text.Json.Serialization;

namespace BookStore.Shared.Models;

/// <summary>
/// Administrative DTO for book information, including all translations.
/// </summary>
public record AdminBookDto(
    Guid Id,
    string Title,
    string? Isbn,
    string Language,
    string LanguageName,
    string? Description,
    PartialDate? PublicationDate,
    bool IsPreRelease,
    PublisherDto? Publisher,
    IReadOnlyList<AuthorDto> Authors,
    IReadOnlyList<CategoryDto> Categories,
    bool IsFavorite,
    int LikeCount,
    float AverageRating,
    int RatingCount,
    int UserRating,
    IReadOnlyDictionary<string, decimal> Prices,
    string? CoverImageUrl,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    BookSale? ActiveSale,
    IReadOnlyList<PriceEntry> CurrentPrices,
    bool IsDeleted,
    IReadOnlyDictionary<string, BookTranslationDto> Translations);
