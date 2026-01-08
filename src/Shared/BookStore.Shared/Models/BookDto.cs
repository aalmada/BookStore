
namespace BookStore.Shared.Models;

/// <summary>
/// DTO for book responses with full related entity details
/// </summary>
public record BookDto(
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
    int LikeCount = 0);
