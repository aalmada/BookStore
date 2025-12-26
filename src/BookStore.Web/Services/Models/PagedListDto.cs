namespace BookStore.Web.Services.Models;

/// <summary>
/// Represents a paginated list response from the API
/// </summary>
public class PagedListDto<T>
{
    public List<T> Items { get; set; } = [];
    public long PageNumber { get; set; }
    public long PageSize { get; set; }
    public long TotalItemCount { get; set; }
    public long PageCount { get; set; }
    public bool HasPreviousPage { get; set; }
    public bool HasNextPage { get; set; }
}
