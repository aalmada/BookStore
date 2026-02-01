using BookStore.Shared.Models;
using Refit;

namespace BookStore.Client;

public interface IGetAllAuthorsAdminEndpoint
{
    [Get("/api/admin/authors")]
    Task<PagedListDto<AdminAuthorDto>> GetAllAuthorsAsync(
        [Query] AuthorSearchRequest request,
        CancellationToken cancellationToken = default);
}
