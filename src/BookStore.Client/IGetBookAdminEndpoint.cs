using Refit;
using System.Threading;
using System.Threading.Tasks;
using BookStore.Shared.Models;

#nullable enable annotations

namespace BookStore.Client
{
    public interface IGetBookAdminEndpoint
    {
        [Headers("Accept: application/json")]
        [Get("/api/admin/books/{id}")]
        Task<AdminBookDto> GetBookAdminAsync(System.Guid id, CancellationToken cancellationToken = default);

        [Headers("Accept: application/json")]
        [Get("/api/admin/books/{id}")]
        Task<IApiResponse<AdminBookDto>> GetBookAdminWithResponseAsync(System.Guid id, CancellationToken cancellationToken = default);
    }
}
