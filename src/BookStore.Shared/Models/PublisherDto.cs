namespace BookStore.Shared.Models;

/// <summary>
/// DTO for publisher information
/// </summary>
public record PublisherDto(
    Guid Id,
    string Name,
    string? ETag = null);
