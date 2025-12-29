namespace BookStore.ApiService.Models;

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

    /// <summary>
    /// Validates and normalizes pagination parameters using configuration options
    /// </summary>
    public PagedRequest Normalize(PaginationOptions options)
    {
        var page = int.Max(DefaultPage, Page ?? DefaultPage);
        var pageSize = int.Clamp(PageSize ?? options.DefaultPageSize, 1, options.MaxPageSize);

        return this with { Page = page, PageSize = pageSize };
    }

    /// <summary>
    /// Calculates the number of items to skip using configuration options
    /// </summary>
    public int GetSkip(PaginationOptions options)
        => ((Page ?? DefaultPage) - 1) * (PageSize ?? options.DefaultPageSize);
}
