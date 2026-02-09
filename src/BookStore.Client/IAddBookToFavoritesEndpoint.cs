using System.Threading;
using System.Threading.Tasks;
using Refit;

namespace BookStore.Client;

public partial interface IAddBookToFavoritesEndpoint
{
    [Headers("Accept: application/json")]
    [Post("/api/books/{id}/favorites")]
    Task AddBookToFavoritesAsync(Guid id, [Header("If-Match")] string? etag = null, CancellationToken cancellationToken = default);
}
