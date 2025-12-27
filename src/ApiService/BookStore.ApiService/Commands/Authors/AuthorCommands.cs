namespace BookStore.ApiService.Commands.Authors;

/// <summary>
/// Command to create a new author
/// </summary>
public record CreateAuthor(
    string Name,
    string? Biography)
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

/// <summary>
/// Command to update an existing author
/// </summary>
public record UpdateAuthor(
    Guid Id,
    string Name,
    string? Biography)
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
