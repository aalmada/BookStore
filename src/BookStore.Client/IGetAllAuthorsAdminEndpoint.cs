using BookStore.Shared.Models;
using Refit;

namespace BookStore.Client;

public interface IGetAllAuthorsAdminEndpoint
{
    [Get("/api/admin/authors")]
    Task<PagedListDto<AuthorDto>> GetAllAuthorsAsync(
        [Query] AuthorSearchRequest request,
        [Header("Accept-Version")] string version,
        [Header("Accept-Language")] string language,
        CancellationToken cancellationToken = default);
}
