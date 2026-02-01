using BookStore.Shared.Models;
using Refit;

namespace BookStore.Client;

public interface IGetAllCategoriesAdminEndpoint
{
    [Get("/api/admin/categories")]
    Task<PagedListDto<AdminCategoryDto>> GetAllCategoriesAsync(
        [Query] CategorySearchRequest request,
        CancellationToken cancellationToken = default);
}
