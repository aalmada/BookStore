using BookStore.Shared.Models;
using Refit;

namespace BookStore.Client;

/// <summary>
/// Client for managing authors in the system.
/// </summary>
public interface IAuthorsClient
{
    /// <summary>
    /// Gets a paged list of authors.
    /// </summary>

    [Headers("Accept: application/json")]
    [Get("/api/authors")]
    Task<PagedListDto<AuthorDto>> GetAuthorsAsync([Query, AliasAs("Page")] int? page, [Query, AliasAs("PageSize")] int? pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific author by its ID.
    /// </summary>
    [Headers("Accept: application/json")]

    [Get("/api/authors/{id}")]
    Task<AuthorDto> GetAuthorAsync(Guid id, [Header("Accept-Language")] string? acceptLanguage = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific author by its ID with full API response.
    /// </summary>
    [Headers("Accept: application/json")]

    [Get("/api/authors/{id}")]
    Task<IApiResponse<AuthorDto>> GetAuthorWithResponseAsync(Guid id, [Header("Accept-Language")] string? acceptLanguage = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all authors for admin management (includes deleted authors).
    /// </summary>
    [Get("/api/admin/authors")]

    Task<PagedListDto<AdminAuthorDto>> GetAllAuthorsAsync(
        [Query] AuthorSearchRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific author by its ID for admin management.
    /// </summary>
    [Headers("Accept: application/json")]

    [Get("/api/admin/authors/{id}")]
    Task<AdminAuthorDto> GetAuthorAdminAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific author by its ID for admin management with full API response.
    /// </summary>
    [Headers("Accept: application/json")]

    [Get("/api/admin/authors/{id}")]
    Task<IApiResponse<AdminAuthorDto>> GetAuthorAdminWithResponseAsync(Guid id, CancellationToken cancellationToken = default);

    [Headers("Content-Type: application/json")]
    [Post("/api/admin/authors")]
    Task CreateAuthorAsync([Body] CreateAuthorRequest body, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new author with full API response (Admin only).
    /// </summary>
    [Headers("Content-Type: application/json")]
    [Post("/api/admin/authors")]
    Task<IApiResponse<AuthorDto>> CreateAuthorWithResponseAsync([Body] CreateAuthorRequest body, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing author (Admin only).
    /// </summary>
    [Headers("Content-Type: application/json")]

    [Put("/api/admin/authors/{id}")]
    Task UpdateAuthorAsync(Guid id, [Body] UpdateAuthorRequest body, [Header("If-Match")] string? etag = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing author with full API response (Admin only).
    /// </summary>
    [Headers("Content-Type: application/json")]

    [Put("/api/admin/authors/{id}")]
    Task<IApiResponse> UpdateAuthorWithResponseAsync(Guid id, [Body] UpdateAuthorRequest body, [Header("If-Match")] string? etag = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft deletes an author (Admin only).
    /// </summary>
    [Delete("/api/admin/authors/{id}")]

    Task SoftDeleteAuthorAsync(Guid id, [Header("If-Match")] string? etag = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft deletes an author with full API response (Admin only).
    /// </summary>
    [Delete("/api/admin/authors/{id}")]

    Task<IApiResponse> SoftDeleteAuthorWithResponseAsync(Guid id, [Header("If-Match")] string? etag = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores a soft-deleted author (Admin only).
    /// </summary>
    [Post("/api/admin/authors/{id}/restore")]

    Task RestoreAuthorAsync(Guid id, [Header("If-Match")] string? etag = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores a soft-deleted author with full API response (Admin only).
    /// </summary>
    [Post("/api/admin/authors/{id}/restore")]
    Task<IApiResponse> RestoreAuthorWithResponseAsync(Guid id, [Header("If-Match")] string? etag = null, CancellationToken cancellationToken = default);
}

