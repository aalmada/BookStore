using Refit;

namespace BookStore.Client;

public interface ICancelBookSaleEndpoint
{
    [Delete("/api/books/{id}/sales")]
    Task CancelBookSaleAsync(Guid id, [Query] DateTimeOffset saleStart);
}
