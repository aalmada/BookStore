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
    List<AuthorDto> Authors,
    List<CategoryDto> Categories);
