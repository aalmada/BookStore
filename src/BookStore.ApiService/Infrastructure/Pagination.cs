namespace BookStore.ApiService.Infrastructure;

/// <summary>
/// Standard pagination request parameters
/// </summary>
public static class PagedRequestExtensions
{
    /// <summary>
    /// Validates and normalizes pagination parameters using configuration options
    /// </summary>
    public static PagedRequest Normalize(this PagedRequest request, PaginationOptions options)
    {
        var page = int.Max(PagedRequest.DefaultPage, request.Page ?? PagedRequest.DefaultPage);
        var pageSize = int.Clamp(request.PageSize ?? options.DefaultPageSize, 1, options.MaxPageSize);

        return request with { Page = page, PageSize = pageSize };
    }

    /// <summary>
    /// Calculates the number of items to skip using configuration options
    /// </summary>
    public static int GetSkip(this PagedRequest request, PaginationOptions options)
        => ((request.Page ?? PagedRequest.DefaultPage) - 1) * (request.PageSize ?? options.DefaultPageSize);
}
