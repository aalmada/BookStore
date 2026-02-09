using Refit;
using System.Threading;
using System.Threading.Tasks;
using BookStore.Shared.Models;

#nullable enable annotations

namespace BookStore.Client
{
    public interface IGetCategoryAdminEndpoint
    {
        [Headers("Accept: application/json")]
        [Get("/api/admin/categories/{id}")]
        Task<AdminCategoryDto> GetCategoryAdminAsync(System.Guid id, CancellationToken cancellationToken = default);

        [Headers("Accept: application/json")]
        [Get("/api/admin/categories/{id}")]
        Task<IApiResponse<AdminCategoryDto>> GetCategoryAdminWithResponseAsync(System.Guid id, CancellationToken cancellationToken = default);
    }
}
