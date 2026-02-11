using System.Threading;
using System.Threading.Tasks;
using BookStore.Shared.Models;
using Refit;

#nullable enable annotations

namespace BookStore.Client;

public interface IGetAuthorAdminEndpoint
{
    [Headers("Accept: application/json")]
    [Get("/api/admin/authors/{id}")]
    Task<AdminAuthorDto> GetAuthorAdminAsync(System.Guid id, CancellationToken cancellationToken = default);

    [Headers("Accept: application/json")]
    [Get("/api/admin/authors/{id}")]
    Task<IApiResponse<AdminAuthorDto>> GetAuthorAdminWithResponseAsync(System.Guid id, CancellationToken cancellationToken = default);
}
