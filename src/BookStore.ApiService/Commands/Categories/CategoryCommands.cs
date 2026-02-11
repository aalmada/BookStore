using BookStore.Shared.Commands;

namespace BookStore.ApiService.Commands;

/// <summary>
/// Command to create a new category
/// </summary>
public record CreateCategory(
    Guid Id,
    IReadOnlyDictionary<string, CategoryTranslationDto> Translations);

/// <summary>
/// Command to update an existing category
/// </summary>
public record UpdateCategory(
    Guid Id,
    IReadOnlyDictionary<string, CategoryTranslationDto> Translations) : IHaveETag
{
    public string? ETag { get; set; }
}

/// <summary>
/// Command to soft delete a category
/// </summary>
public record SoftDeleteCategory(Guid Id) : IHaveETag
{
    public string? ETag { get; set; }
}

/// <summary>
/// Command to restore a soft deleted category
/// </summary>
public record RestoreCategory(Guid Id) : IHaveETag
{
    public string? ETag { get; set; }
}
