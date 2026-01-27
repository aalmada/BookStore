namespace BookStore.Shared.Models;

/// <summary>
/// Standard pagination request parameters
/// </summary>
public record PagedRequest
{
    /// <summary>
    /// Default page number (1-based)
    /// </summary>
    public const int DefaultPage = 1;

    /// <summary>
    /// Page number (1-based)
    /// </summary>
    public int? Page { get; init; }

    /// <summary>
    /// Number of items per page
    /// </summary>
    public int? PageSize { get; init; }
}
