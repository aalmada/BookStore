using System.Threading;
using System.Threading.Tasks;
using Refit;

namespace BookStore.Client;

[System.CodeDom.Compiler.GeneratedCode("Refitter", "1.7.1.0")]
public partial interface IAddBookToFavoritesEndpoint
{
    [Headers("Accept: application/json")]
    [Post("/api/books/{id}/favorites")]
    Task AddBookToFavoritesAsync(Guid id, CancellationToken cancellationToken = default);
}
