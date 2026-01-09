using Refit;

namespace BookStore.Client;

[Headers("api-version: 1.0")]
public partial interface IRateBookEndpoint
{
    [Post("/api/books/{id}/rating")]
    Task Execute(Guid id, [Body] RateBookRequest request);
}

public record RateBookRequest(int Rating);
