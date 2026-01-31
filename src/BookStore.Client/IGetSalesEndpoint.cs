using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BookStore.Shared.Models;
using Refit;

namespace BookStore.Client;

public interface IGetSalesEndpoint
{
    [Get("/api/sales")]
    Task<PagedListDto<SaleDto>> GetSalesAsync(
        [Query, AliasAs("Page")] int? page,
        [Query, AliasAs("PageSize")] int? pageSize,
        [Header("api-version")] string api_version,
        [Header("Accept-Language")] string accept_Language,
        [Header("X-Correlation-ID")] string? x_Correlation_ID = null,
        [Header("X-Causation-ID")] string? x_Causation_ID = null,
        CancellationToken cancellationToken = default);
}
