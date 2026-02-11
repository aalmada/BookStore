using BookStore.Shared.Models;
using Refit;

namespace BookStore.Client;

/// <summary>
/// Client for managing categories in the system.
/// </summary>
public interface ICategoriesClient
{
    /// <summary>
    /// Gets a paged list of categories.
    /// </summary>

    [Headers("Accept: application/json")]
    [Get("/api/categories")]
    Task<PagedListDto<CategoryDto>> GetCategoriesAsync([Query, AliasAs("Page")] int? page, [Query, AliasAs("PageSize")] int? pageSize, [Query] string? sortBy, [Query] string? sortOrder, CancellationToken cancellationToken = default);

    [Headers("Content-Type: application/json")]
    [Get("/api/categories/{id}")]
    Task<CategoryDto> GetCategoryAsync(Guid id, [Header("If-None-Match")] string? ifNoneMatch = default, [Header("Accept-Language")] string? acceptLanguage = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific category by its ID with full API response.
    /// </summary>
    [Headers("Content-Type: application/json")]

    [Get("/api/categories/{id}")]
    Task<IApiResponse<CategoryDto>> GetCategoryWithResponseAsync(Guid id, [Header("If-None-Match")] string? ifNoneMatch = default, [Header("Accept-Language")] string? acceptLanguage = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all categories for admin management (includes deleted categories).
    /// </summary>
    [Get("/api/admin/categories")]

    Task<PagedListDto<AdminCategoryDto>> GetAllCategoriesAsync(
        [Query] CategorySearchRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific category by its ID for admin management.
    /// </summary>
    [Headers("Accept: application/json")]

    [Get("/api/admin/categories/{id}")]
    Task<AdminCategoryDto> GetCategoryAdminAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific category by its ID for admin management with full API response.
    /// </summary>
    [Headers("Accept: application/json")]

    [Get("/api/admin/categories/{id}")]
    Task<IApiResponse<AdminCategoryDto>> GetCategoryAdminWithResponseAsync(Guid id, CancellationToken cancellationToken = default);

    [Headers("Content-Type: application/json")]
    [Post("/api/admin/categories")]
    Task CreateCategoryAsync([Body] CreateCategoryRequest body, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new category with full API response (Admin only).
    /// </summary>
    [Headers("Content-Type: application/json")]
    [Post("/api/admin/categories")]
    Task<IApiResponse<CategoryDto>> CreateCategoryWithResponseAsync([Body] CreateCategoryRequest body, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing category (Admin only).
    /// </summary>
    [Headers("Content-Type: application/json")]

    [Put("/api/admin/categories/{id}")]
    Task UpdateCategoryAsync(Guid id, [Body] UpdateCategoryRequest body, [Header("If-Match")] string? etag = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing category with full API response (Admin only).
    /// </summary>
    [Headers("Content-Type: application/json")]

    [Put("/api/admin/categories/{id}")]
    Task<IApiResponse> UpdateCategoryWithResponseAsync(Guid id, [Body] UpdateCategoryRequest body, [Header("If-Match")] string? etag = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft deletes a category (Admin only).
    /// </summary>
    [Delete("/api/admin/categories/{id}")]

    Task SoftDeleteCategoryAsync(Guid id, [Header("If-Match")] string? etag = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft deletes a category with full API response (Admin only).
    /// </summary>
    [Delete("/api/admin/categories/{id}")]

    Task<IApiResponse> SoftDeleteCategoryWithResponseAsync(Guid id, [Header("If-Match")] string? etag = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores a soft-deleted category (Admin only).
    /// </summary>
    [Post("/api/admin/categories/{id}/restore")]

    Task RestoreCategoryAsync(Guid id, [Header("If-Match")] string? etag = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores a soft-deleted category with full API response (Admin only).
    /// </summary>
    [Post("/api/admin/categories/{id}/restore")]
    Task<IApiResponse> RestoreCategoryWithResponseAsync(Guid id, [Header("If-Match")] string? etag = null, CancellationToken cancellationToken = default);
}

