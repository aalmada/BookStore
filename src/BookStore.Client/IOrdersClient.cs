using BookStore.Shared.Models;
using Refit;

namespace BookStore.Client;

[Headers("api-version: 1.0")]
public interface IOrdersClient
{
    [Post("/api/orders")]
    Task<IApiResponse<OrderSummaryDto>> PlaceOrderAsync(
        [Body] PlaceOrderRequest request,
        CancellationToken cancellationToken = default);

    [Get("/api/orders")]
    Task<IApiResponse<IReadOnlyList<OrderSummaryDto>>> GetOrdersAsync(
        CancellationToken cancellationToken = default);
}
