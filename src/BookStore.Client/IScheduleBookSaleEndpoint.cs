using Refit;

namespace BookStore.Client;

public interface IScheduleBookSaleEndpoint
{
    [Post("/api/books/{id}/sales")]
    Task ScheduleBookSaleAsync(Guid id, [Body] ScheduleSaleRequest request, [Header("If-Match")] string? etag = null);

    [Post("/api/books/{id}/sales")]
    Task<IApiResponse> ScheduleBookSaleWithResponseAsync(Guid id, [Body] ScheduleSaleRequest request, [Header("If-Match")] string? etag = null);
}

public record ScheduleSaleRequest(decimal Percentage, DateTimeOffset Start, DateTimeOffset End);
