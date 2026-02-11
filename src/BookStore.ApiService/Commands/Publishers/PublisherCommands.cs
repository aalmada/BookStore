using BookStore.Shared.Commands;

namespace BookStore.ApiService.Commands;

/// <summary>
/// Command to create a new publisher
/// </summary>
public record CreatePublisher(Guid Id, string Name);

/// <summary>
/// Command to update an existing publisher
/// </summary>
public record UpdatePublisher(
    Guid Id,
    string Name) : IHaveETag
{
    public string? ETag { get; set; }
}

/// <summary>
/// Command to soft delete a publisher
/// </summary>
public record SoftDeletePublisher(Guid Id) : IHaveETag
{
    public string? ETag { get; set; }
}

/// <summary>
/// Command to restore a soft deleted publisher
/// </summary>
public record RestorePublisher(Guid Id) : IHaveETag
{
    public string? ETag { get; set; }
}
