namespace BookStore.ApiService.Models;

/// <summary>
/// Configuration options for pagination
/// </summary>
public sealed class PaginationOptions
{
    /// <summary>
    /// Configuration section name
    /// </summary>
    public const string SectionName = "Pagination";

    /// <summary>
    /// Default value for page size when not specified
    /// </summary>
    public const int DefaultPageSizeValue = 20;

    /// <summary>
    /// Default value for maximum number of items allowed per page
    /// </summary>
    public const int MaxPageSizeValue = 100;

    /// <summary>
    /// Default page size when not specified
    /// </summary>
    public int DefaultPageSize { get; init; } = DefaultPageSizeValue;

    /// <summary>
    /// Maximum number of items allowed per page
    /// </summary>
    public int MaxPageSize { get; init; } = MaxPageSizeValue;
}
