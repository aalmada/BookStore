namespace BookStore.Shared.Models;

/// <summary>
/// Represents a paginated list response for API clients
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

    public PagedListDto()
    {
    }

    public PagedListDto(List<T> items, long pageNumber, long pageSize, long totalItemCount)
    {
        Items = items;
        PageNumber = pageNumber;
        PageSize = pageSize;
        TotalItemCount = totalItemCount;
        PageCount = (long)Math.Ceiling(totalItemCount / (double)pageSize);
        HasPreviousPage = pageNumber > 1;
        HasNextPage = pageNumber < PageCount;
    }
}
