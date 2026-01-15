using Refit;

namespace BookStore.Client;

public interface IScheduleBookSaleEndpoint
{
    [Post("/api/books/{id}/sales")]
    Task ScheduleBookSaleAsync(Guid id, [Body] ScheduleSaleRequest request);
}

public record ScheduleSaleRequest(decimal Percentage, DateTimeOffset Start, DateTimeOffset End);
