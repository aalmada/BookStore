namespace BookStore.ApiService.Commands;

/// <summary>
/// Command to create a new author
/// </summary>
public record CreateAuthor(
    string Name,
    IReadOnlyDictionary<string, AuthorTranslationDto>? Translations)
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

/// <summary>
/// DTO for localized author biographies
/// </summary>
public record AuthorTranslationDto(string Biography);

/// <summary>
/// Command to update an existing author
/// </summary>
public record UpdateAuthor(
    Guid Id,
    string Name,
    IReadOnlyDictionary<string, AuthorTranslationDto>? Translations)
{
    public string? ETag { get; init; }
}

/// <summary>
/// Command to soft delete an author
/// </summary>
public record SoftDeleteAuthor(Guid Id)
{
    public string? ETag { get; init; }
}

/// <summary>
/// Command to restore a soft deleted author
/// </summary>
public record RestoreAuthor(Guid Id)
{
    public string? ETag { get; init; }
}
