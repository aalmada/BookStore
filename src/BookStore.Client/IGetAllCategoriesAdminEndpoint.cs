using BookStore.Shared.Models;
using Refit;

namespace BookStore.Client;

public interface IGetAllCategoriesAdminEndpoint
{
    [Get("/api/admin/categories")]
    Task<PagedListDto<CategoryDto>> GetAllCategoriesAsync(
        [Query] CategorySearchRequest request,
        [Header("Accept-Version")] string version,
        [Header("Accept-Language")] string language,
        CancellationToken cancellationToken = default);
}
