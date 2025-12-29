namespace BookStore.Shared.Models;

/// <summary>
/// DTO for category information (localized based on Accept-Language header)
/// </summary>
public record CategoryDto(
    Guid Id,
    string Name);
