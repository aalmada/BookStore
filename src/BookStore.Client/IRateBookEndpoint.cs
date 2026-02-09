using Refit;

namespace BookStore.Client;

[Headers("api-version: 1.0")]
public partial interface IRateBookEndpoint
{
    [Post("/api/books/{id}/rating")]
    Task RateBookAsync(Guid id, [Body] RateBookRequest request, [Header("If-Match")] string? etag = null);
}

public record RateBookRequest(int Rating);
