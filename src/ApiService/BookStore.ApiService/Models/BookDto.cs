namespace BookStore.ApiService.Models;

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

/// <summary>
/// DTO for publisher information
/// </summary>
public record PublisherDto(
    Guid Id,
    string Name);

/// <summary>
/// DTO for author information
/// </summary>
public record AuthorDto(
    Guid Id,
    string Name,
    string? Biography);

/// <summary>
/// DTO for category information (localized based on Accept-Language header)
/// </summary>
public record CategoryDto(
    Guid Id,
    string Name);
