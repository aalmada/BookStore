namespace BookStore.ApiService.Models;

/// <summary>
/// Standard pagination request parameters
/// </summary>
public record PagedRequest
{
    /// <summary>
    /// Maximum number of items allowed per page
    /// </summary>
    public const int MaxPageSize = 100;
    
    /// <summary>
    /// Page number (1-based)
    /// </summary>
    public int Page { get; init; } = 1;
    
    /// <summary>
    /// Number of items per page
    /// </summary>
    public int PageSize { get; init; } = 20;
    
    /// <summary>
    /// Validates and normalizes pagination parameters
    /// </summary>
    public PagedRequest Normalize()
    {
        var page = int.Max(1, Page);
        var pageSize = int.Clamp(PageSize, 1, MaxPageSize);
        
        return this with { Page = page, PageSize = pageSize };
    }
    
    /// <summary>
    /// Calculates the number of items to skip
    /// </summary>
    public int Skip => (Page - 1) * PageSize;
}
