using Marten.Pagination;
using ApiServiceModels = BookStore.ApiService.Models;

namespace BookStore.ApiService.Infrastructure.Extensions;

public static class PagedListExtensions
{
    /// <summary>
    /// Creates a PagedListDto from Marten's IPagedList
    /// </summary>
    public static PagedListDto<T> ToPagedListDto<T>(this IPagedList<T> pagedList) => new(
        [.. pagedList],
        pagedList.PageNumber,
        pagedList.PageSize,
        pagedList.TotalItemCount);
}
