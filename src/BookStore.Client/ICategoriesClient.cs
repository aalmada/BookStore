using BookStore.Shared.Models;
using Refit;

namespace BookStore.Client;

public interface ICategoriesClient
{
    [Headers("Accept: application/json")]
    [Get("/api/categories")]
    Task<PagedListDto<CategoryDto>> GetCategoriesAsync([Query, AliasAs("Page")] int? page, [Query, AliasAs("PageSize")] int? pageSize, [Query] string? sortBy, [Query] string? sortOrder, CancellationToken cancellationToken = default);

    [Headers("Content-Type: application/json")]
    [Get("/api/categories/{id}")]
    Task<CategoryDto> GetCategoryAsync(Guid id, [Header("If-None-Match")] string? ifNoneMatch = default, [Header("Accept-Language")] string? acceptLanguage = default, CancellationToken cancellationToken = default);

    [Headers("Content-Type: application/json")]
    [Get("/api/categories/{id}")]
    Task<IApiResponse<CategoryDto>> GetCategoryWithResponseAsync(Guid id, [Header("If-None-Match")] string? ifNoneMatch = default, [Header("Accept-Language")] string? acceptLanguage = default, CancellationToken cancellationToken = default);

    [Get("/api/admin/categories")]
    Task<PagedListDto<AdminCategoryDto>> GetAllCategoriesAsync(
        [Query] CategorySearchRequest request,
        CancellationToken cancellationToken = default);

    [Headers("Accept: application/json")]
    [Get("/api/admin/categories/{id}")]
    Task<AdminCategoryDto> GetCategoryAdminAsync(Guid id, CancellationToken cancellationToken = default);

    [Headers("Accept: application/json")]
    [Get("/api/admin/categories/{id}")]
    Task<IApiResponse<AdminCategoryDto>> GetCategoryAdminWithResponseAsync(Guid id, CancellationToken cancellationToken = default);

    [Headers("Content-Type: application/json")]
    [Post("/api/admin/categories")]
    Task CreateCategoryAsync([Body] CreateCategoryRequest body, CancellationToken cancellationToken = default);

    [Headers("Content-Type: application/json")]
    [Post("/api/admin/categories")]
    Task<ApiResponse<CategoryDto>> CreateCategoryWithResponseAsync([Body] CreateCategoryRequest body, CancellationToken cancellationToken = default);

    [Headers("Content-Type: application/json")]
    [Put("/api/admin/categories/{id}")]
    Task UpdateCategoryAsync(Guid id, [Body] UpdateCategoryRequest body, [Header("If-Match")] string? etag = null, CancellationToken cancellationToken = default);

    [Headers("Content-Type: application/json")]
    [Put("/api/admin/categories/{id}")]
    Task<IApiResponse> UpdateCategoryWithResponseAsync(Guid id, [Body] UpdateCategoryRequest body, [Header("If-Match")] string? etag = null, CancellationToken cancellationToken = default);

    [Delete("/api/admin/categories/{id}")]
    Task SoftDeleteCategoryAsync(Guid id, [Header("If-Match")] string? etag = null, CancellationToken cancellationToken = default);

    [Delete("/api/admin/categories/{id}")]
    Task<IApiResponse> SoftDeleteCategoryWithResponseAsync(Guid id, [Header("If-Match")] string? etag = null, CancellationToken cancellationToken = default);

    [Post("/api/admin/categories/{id}/restore")]
    Task RestoreCategoryAsync(Guid id, [Header("If-Match")] string? etag = null, CancellationToken cancellationToken = default);

    [Post("/api/admin/categories/{id}/restore")]
    Task<IApiResponse> RestoreCategoryWithResponseAsync(Guid id, [Header("If-Match")] string? etag = null, CancellationToken cancellationToken = default);
}
