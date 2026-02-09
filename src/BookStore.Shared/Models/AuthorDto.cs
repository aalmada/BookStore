namespace BookStore.Shared.Models;

/// <summary>
/// DTO for author information
/// </summary>
public record AuthorDto(
    Guid Id,
    string Name,
    string? Biography,
    string? ETag = null);

public record AuthorTranslationDto(string Biography);
