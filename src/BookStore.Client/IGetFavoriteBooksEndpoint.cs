using System.Threading;
using System.Threading.Tasks;
using BookStore.Shared.Models;
using Refit;

namespace BookStore.Client;

public partial interface IGetFavoriteBooksEndpoint
{
    [Headers("Accept: application/json")]
    [Get("/api/books/favorites")]
    Task<PagedListDto<BookDto>> GetFavoriteBooksAsync(
        [Query] OrderedPagedRequest request,
        CancellationToken cancellationToken = default);
}
