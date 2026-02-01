using System.Threading;
using System.Threading.Tasks;
using Refit;

namespace BookStore.Client;

public partial interface IRemoveBookFromFavoritesEndpoint
{
    [Headers("Accept: application/json")]
    [Delete("/api/books/{id}/favorites")]
    Task RemoveBookFromFavoritesAsync(Guid id, CancellationToken cancellationToken = default);
}
