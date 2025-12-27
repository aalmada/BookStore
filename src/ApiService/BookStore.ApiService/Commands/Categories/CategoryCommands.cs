namespace BookStore.ApiService.Commands.Categories;

/// <summary>
/// Command to create a new category
/// </summary>
public record CreateCategory(
    string Name,
    string? Description,
    Dictionary<string, CategoryTranslationDto> Translations)
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

/// <summary>
/// DTO for category translations
/// </summary>
public record CategoryTranslationDto(string Name, string? Description);

/// <summary>
/// Command to update an existing category
/// </summary>
public record UpdateCategory(
    Guid Id,
    string Name,
    string? Description,
    Dictionary<string, CategoryTranslationDto> Translations)
{
    public string? ETag { get; init; }
}

/// <summary>
/// Command to soft delete a category
/// </summary>
public record SoftDeleteCategory(Guid Id)
{
    public string? ETag { get; init; }
}

/// <summary>
/// Command to restore a soft deleted category
/// </summary>
public record RestoreCategory(Guid Id)
{
    public string? ETag { get; init; }
}
