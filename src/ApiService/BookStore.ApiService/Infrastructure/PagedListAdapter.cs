using System.Text.Json.Serialization;
using Marten.Pagination;

namespace BookStore.ApiService.Infrastructure;

/// <summary>
/// DTO for paged list responses that supports serialization/deserialization.
/// </summary>
public sealed record PagedListAdapter<T>
{
    [JsonConstructor]
    public PagedListAdapter(IEnumerable<T> items, long pageNumber, long pageSize, long totalItemCount)
    {
        Items = items ?? [];
        PageNumber = pageNumber;
        PageSize = pageSize;
        TotalItemCount = totalItemCount;
    }

    public PagedListAdapter(IPagedList<T> source) : this(source, source.PageNumber, source.PageSize, source.TotalItemCount)
    {
    }

    public IEnumerable<T> Items { get; init; }
    public long PageNumber { get; init; }
    public long PageSize { get; init; }
    public long TotalItemCount { get; init; }

    public long PageCount => PageSize > 0 ? (long)Math.Ceiling(TotalItemCount / (double)PageSize) : 0;
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < PageCount;
}
