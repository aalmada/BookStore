using System.Threading;
using System.Threading.Tasks;
using Refit;

namespace BookStore.Client;

[System.CodeDom.Compiler.GeneratedCode("Refitter", "1.7.1.0")]
public partial interface IRemoveBookFromFavoritesEndpoint
{
    [Headers("Accept: application/json")]
    [Delete("/api/books/{id}/favorites")]
    Task Execute([AliasAs("id")] Guid id, CancellationToken cancellationToken = default);
}
