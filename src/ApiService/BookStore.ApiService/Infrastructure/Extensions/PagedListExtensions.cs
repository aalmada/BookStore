using BookStore.Shared.Models;
using Marten.Pagination;

namespace BookStore.ApiService.Infrastructure.Extensions;

public static class PagedListExtensions
{
    /// <summary>
    /// Creates a PagedListDto from Marten's IPagedList
    /// </summary>
    public static PagedListDto<T> ToPagedListDto<T>(this IPagedList<T> pagedList) => new()
    {
        Items = [.. pagedList],
        PageNumber = pagedList.PageNumber,
        PageSize = pagedList.PageSize,
        TotalItemCount = pagedList.TotalItemCount,
        PageCount = pagedList.PageCount,
        HasPreviousPage = pagedList.HasPreviousPage,
        HasNextPage = pagedList.HasNextPage
    };
}
