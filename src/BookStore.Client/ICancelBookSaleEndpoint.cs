using Refit;

namespace BookStore.Client;

    public interface ICancelBookSaleEndpoint
    {
        [Delete("/api/books/{id}/sales")]
        Task CancelBookSaleAsync(Guid id, [Query] DateTimeOffset saleStart, [Header("If-Match")] string? etag = null);

        [Delete("/api/books/{id}/sales")]
        Task<IApiResponse> CancelBookSaleWithResponseAsync(Guid id, [Query] DateTimeOffset saleStart, [Header("If-Match")] string? etag = null);
    }
