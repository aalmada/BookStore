using System.Collections;
using Marten.Pagination;

namespace BookStore.ApiService.Infrastructure;

/// <summary>
/// Zero-allocation adapter that wraps Marten's IPagedList for efficient serialization.
/// This immutable record delegates to the source without copying items, avoiding GC pressure.
/// </summary>
public sealed record PagedListAdapter<T>(IPagedList<T> Source) : IReadOnlyList<T>
{
    // Pagination metadata - delegates to source
    public long PageNumber => Source.PageNumber;
    public long PageSize => Source.PageSize;
    public long TotalItemCount => Source.TotalItemCount;
    public long PageCount => Source.PageCount;
    public bool HasPreviousPage => Source.HasPreviousPage;
    public bool HasNextPage => Source.HasNextPage;

    // IReadOnlyList implementation - delegates to source
    public int Count => (int)Source.Count;
    public T this[int index] => Source[index];
    public IEnumerator<T> GetEnumerator() => Source.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
