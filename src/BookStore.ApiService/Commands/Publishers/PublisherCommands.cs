namespace BookStore.ApiService.Commands.Publishers;

/// <summary>
/// Command to create a new publisher
/// </summary>
public record CreatePublisher(string Name)
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

/// <summary>
/// Command to update an existing publisher
/// </summary>
public record UpdatePublisher(
    Guid Id,
    string Name)
{
    public string? ETag { get; init; }
}

/// <summary>
/// Command to soft delete a publisher
/// </summary>
public record SoftDeletePublisher(Guid Id)
{
    public string? ETag { get; init; }
}

/// <summary>
/// Command to restore a soft deleted publisher
/// </summary>
public record RestorePublisher(Guid Id)
{
    public string? ETag { get; init; }
}
