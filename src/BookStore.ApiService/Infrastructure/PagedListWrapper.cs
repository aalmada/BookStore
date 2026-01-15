using System.Collections;
using Marten.Pagination;

namespace BookStore.ApiService.Infrastructure;

/// <summary>
/// Simple wrapper that implements IPagedList for mapped collections.
/// Used to wrap DTO collections with pagination metadata.
/// </summary>
sealed class PagedListWrapper<T> : IPagedList<T>
{
    readonly IReadOnlyList<T> _items;

    public PagedListWrapper(
        IReadOnlyList<T> items,
        long pageNumber,
        long pageSize,
        long totalItemCount)
    {
        _items = items;
        PageNumber = pageNumber;
        PageSize = pageSize;
        TotalItemCount = totalItemCount;
    }

    public long PageNumber { get; }
    public long PageSize { get; }
    public long TotalItemCount { get; }
    public long PageCount => (long)double.Ceiling(TotalItemCount / (double)PageSize);
    public bool IsFirstPage => PageNumber == 1;
    public bool IsLastPage => PageNumber >= PageCount;
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < PageCount;
    public long FirstItemOnPage => ((PageNumber - 1) * PageSize) + 1;
    public long LastItemOnPage => long.Min(PageNumber * PageSize, TotalItemCount);

    // IPagedList<T>.Count returns long
    long IPagedList<T>.Count => _items.Count;

    // IReadOnlyList<T>.Count returns int
    public int Count => _items.Count;

    public T this[int index] => _items[index];
    public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
