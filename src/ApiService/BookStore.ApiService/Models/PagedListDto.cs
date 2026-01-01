namespace BookStore.ApiService.Models;

/// <summary>
/// Represents a paginated list response for API clients
/// </summary>
public record PagedListDto<T>(
    IReadOnlyList<T> Items,
    long PageNumber,
    long PageSize,
    long TotalItemCount)
{
    public long PageCount { get; init; } = (long)double.Ceiling(TotalItemCount / (double)PageSize);
    public bool HasPreviousPage { get; init; } = PageNumber > 1;
    public bool HasNextPage { get; init; } = PageNumber < (long)double.Ceiling(TotalItemCount / (double)PageSize);
}
