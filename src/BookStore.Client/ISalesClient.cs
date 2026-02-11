using BookStore.Shared.Models;
using Refit;

namespace BookStore.Client;

/// <summary>
/// Client for managing sales in the system.
/// </summary>
public interface ISalesClient
{
    /// <summary>
    /// Gets a paged list of sales.
    /// </summary>

    [Get("/api/sales")]
    Task<PagedListDto<SaleDto>> GetSalesAsync(
        [Query, AliasAs("Page")] int? page,
        [Query, AliasAs("PageSize")] int? pageSize,
        CancellationToken cancellationToken = default);
}

